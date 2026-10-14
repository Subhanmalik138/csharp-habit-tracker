using HabitTracker.Core;
using Xunit;

namespace HabitTracker.Tests;

public sealed class JsonHabitRepositoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private string PathFor(string name) => Path.Combine(_dir, name);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void RoundTrip_Preserves_Habits_And_CheckIns()
    {
        var repo = new JsonHabitRepository(PathFor("habits.json"));
        var habit = new Habit
        {
            Name = "Read",
            Description = "20 pages a day",
            Frequency = HabitFrequency.Daily
        };
        habit.CheckIn(new DateOnly(2026, 10, 14), "chapter 4");
        repo.Add(habit);

        var reloaded = new JsonHabitRepository(PathFor("habits.json"));
        var all = reloaded.GetAll();

        var loaded = Assert.Single(all);
        Assert.Equal("Read", loaded.Name);
        Assert.Equal("20 pages a day", loaded.Description);
        Assert.Equal(HabitFrequency.Daily, loaded.Frequency);
        var checkIn = Assert.Single(loaded.CheckIns);
        Assert.Equal(new DateOnly(2026, 10, 14), checkIn.Date);
        Assert.Equal("chapter 4", checkIn.Note);
    }

    [Fact]
    public void Update_Persists_Changes()
    {
        var file = PathFor("habits.json");
        var repo = new JsonHabitRepository(file);
        var habit = new Habit { Name = "Run" };
        repo.Add(habit);

        habit.CheckIn(new DateOnly(2026, 10, 14));
        repo.Update(habit);

        var reloaded = new JsonHabitRepository(file);
        Assert.Single(reloaded.GetById(habit.Id)!.CheckIns);
    }

    [Fact]
    public void Delete_Removes_Habit()
    {
        var file = PathFor("habits.json");
        var repo = new JsonHabitRepository(file);
        var habit = new Habit { Name = "Meditate" };
        repo.Add(habit);

        repo.Delete(habit.Id);

        Assert.Null(repo.GetById(habit.Id));
        Assert.Empty(new JsonHabitRepository(file).GetAll());
    }

    [Fact]
    public void Corrupt_File_Throws_InvalidDataException()
    {
        var file = PathFor("habits.json");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(file, "{ not valid json");

        Assert.Throws<InvalidDataException>(() => new JsonHabitRepository(file).GetAll());
    }

    [Fact]
    public void Missing_File_Starts_Empty()
    {
        var repo = new JsonHabitRepository(PathFor("habits.json"));

        Assert.Empty(repo.GetAll());
    }
}
