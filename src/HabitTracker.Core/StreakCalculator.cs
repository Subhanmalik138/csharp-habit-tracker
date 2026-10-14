namespace HabitTracker.Core;

/// <summary>
/// Streak statistics for one habit, computed as of a reference date.
/// </summary>
public sealed record StreakReport(
    int CurrentStreak,
    int LongestStreak,
    double CompletionRate,
    int TotalPeriods,
    int CompletedPeriods);

/// <summary>
/// Pure streak math over a habit's check-ins. Works on calendar days
/// (or ISO weeks for weekly habits), so multiple check-ins in one
/// period never inflate a streak.
/// </summary>
public static class StreakCalculator
{
    public static StreakReport Analyze(Habit habit, DateOnly today)
    {
        int stepDays = habit.Frequency == HabitFrequency.Daily ? 1 : 7;

        var periods = habit.Frequency == HabitFrequency.Daily
            ? habit.CheckIns.Select(c => c.Date)
            : habit.CheckIns.Select(c => StartOfIsoWeek(c.Date));

        var set = new HashSet<DateOnly>(periods);
        if (set.Count == 0)
            return new StreakReport(0, 0, 0, 0, 0);

        var sorted = set.Order().ToList();

        // Periods are anchored on day starts (daily) or Monday week starts
        // (weekly); normalize "today" the same way before walking back.
        var anchor = habit.Frequency == HabitFrequency.Daily
            ? today
            : StartOfIsoWeek(today);

        // The current streak stays alive if the most recent required period
        // (this period, or the previous one) was completed.
        if (!set.Contains(anchor))
            anchor = anchor.AddDays(-stepDays);

        int current = 0;
        var cursor = anchor;
        while (set.Contains(cursor))
        {
            current++;
            cursor = cursor.AddDays(-stepDays);
        }

        // Longest run of consecutive completed periods, anywhere in history.
        int longest = 1, run = 1;
        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i] == sorted[i - 1].AddDays(stepDays))
                run++;
            else
            {
                longest = Math.Max(longest, run);
                run = 1;
            }
        }
        longest = Math.Max(longest, run);

        // Completion rate: completed periods vs. periods elapsed since creation.
        var first = habit.Frequency == HabitFrequency.Daily
            ? habit.CreatedAt
            : StartOfIsoWeek(habit.CreatedAt);
        int totalPeriods = 0;
        for (var d = first; d <= anchor; d = d.AddDays(stepDays))
            totalPeriods++;
        totalPeriods = Math.Max(totalPeriods, 1);

        double rate = (double)set.Count / totalPeriods;

        return new StreakReport(current, longest, Math.Round(rate, 3), totalPeriods, set.Count);
    }

    /// <summary>
    /// True when the habit already has a check-in covering <paramref name="date"/>.
    /// </summary>
    public static bool IsDoneFor(Habit habit, DateOnly date) =>
        habit.Frequency == HabitFrequency.Daily
            ? habit.CheckIns.Any(c => c.Date == date)
            : habit.CheckIns.Any(c => StartOfIsoWeek(c.Date) == StartOfIsoWeek(date));

    private static DateOnly StartOfIsoWeek(DateOnly date)
    {
        // ISO weeks start on Monday; DayOfWeek.Sunday == 0 needs the -6 wrap.
        int offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }
}
