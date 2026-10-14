using System.Text.Json;
using System.Text.Json.Serialization;

namespace HabitTracker.Core;

/// <summary>
/// JSON-file-backed habit repository. Writes are atomic (temp file +
/// rename) so a crash mid-save can never leave a half-written store,
/// and a process-wide lock keeps concurrent CLI invocations safe.
/// </summary>
public sealed class JsonHabitRepository : IHabitRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _filePath;
    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, Habit> _habits = new();
    private bool _loaded;

    public JsonHabitRepository(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    public IReadOnlyList<Habit> GetAll()
    {
        EnsureLoaded();
        lock (_lock)
            return _habits.Values.OrderBy(h => h.CreatedAt).ToList();
    }

    public Habit? GetById(Guid id)
    {
        EnsureLoaded();
        lock (_lock)
            return _habits.TryGetValue(id, out var habit) ? habit : null;
    }

    public void Add(Habit habit)
    {
        ArgumentNullException.ThrowIfNull(habit);
        EnsureLoaded();
        lock (_lock)
        {
            if (_habits.ContainsKey(habit.Id))
                throw new InvalidOperationException($"Habit {habit.Id} already exists.");
            _habits[habit.Id] = habit;
        }
        Save();
    }

    public void Update(Habit habit)
    {
        ArgumentNullException.ThrowIfNull(habit);
        EnsureLoaded();
        lock (_lock)
        {
            if (!_habits.ContainsKey(habit.Id))
                throw new KeyNotFoundException($"Habit {habit.Id} not found.");
            _habits[habit.Id] = habit;
        }
        Save();
    }

    public void Delete(Guid id)
    {
        EnsureLoaded();
        lock (_lock)
        {
            if (!_habits.Remove(id))
                throw new KeyNotFoundException($"Habit {id} not found.");
        }
        Save();
    }

    public void Save()
    {
        lock (_lock)
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var payload = JsonSerializer.Serialize(_habits.Values.ToList(), JsonOptions);

            // Atomic write: a reader never sees a torn file.
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, payload);
            File.Move(tempPath, _filePath, overwrite: true);
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded)
            return;

        lock (_lock)
        {
            if (_loaded)
                return;

            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    var habits = JsonSerializer.Deserialize<List<Habit>>(json, JsonOptions);
                    if (habits is not null)
                        foreach (var habit in habits)
                            _habits[habit.Id] = habit;
                }
                catch (JsonException ex)
                {
                    throw new InvalidDataException(
                        $"Habit data file '{_filePath}' is corrupt and cannot be loaded.", ex);
                }
            }

            _loaded = true;
        }
    }
}
