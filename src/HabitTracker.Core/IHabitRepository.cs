namespace HabitTracker.Core;

/// <summary>
/// Storage contract for habits. Implementations own how habits are
/// persisted; the rest of the app only speaks this interface.
/// </summary>
public interface IHabitRepository
{
    IReadOnlyList<Habit> GetAll();
    Habit? GetById(Guid id);
    void Add(Habit habit);
    void Update(Habit habit);
    void Delete(Guid id);
    void Save();
}
