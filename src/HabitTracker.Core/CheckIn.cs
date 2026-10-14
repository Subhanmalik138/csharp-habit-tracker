namespace HabitTracker.Core;

/// <summary>
/// A single recorded completion of a habit on a given calendar date.
/// Dates are compared by day (or ISO week), never by timestamp, so
/// multiple check-ins on the same day count once for streak purposes.
/// </summary>
public sealed class CheckIn
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateOnly Date { get; init; }

    public string? Note { get; set; }

    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;
}
