namespace HabitTracker.Core;

/// <summary>
/// A habit the user wants to build: its identity, cadence, and the
/// log of days (or weeks) it was actually completed.
/// </summary>
public sealed class Habit
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public HabitFrequency Frequency { get; set; } = HabitFrequency.Daily;

    public DateOnly CreatedAt { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public List<CheckIn> CheckIns { get; init; } = new();

    /// <summary>
    /// Records a completion for the given date. Returns false if that
    /// date was already checked in (idempotent - no duplicates).
    /// </summary>
    public bool CheckIn(DateOnly date, string? note = null)
    {
        if (CheckIns.Any(c => c.Date == date))
            return false;

        CheckIns.Add(new CheckIn { Date = date, Note = note });
        return true;
    }
}
