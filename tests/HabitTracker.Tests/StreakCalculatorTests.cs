using HabitTracker.Core;
using Xunit;

namespace HabitTracker.Tests;

public sealed class StreakCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 14); // a Wednesday

    private static Habit DailyHabit(params string[] isoDates)
    {
        var habit = new Habit { Name = "test", Frequency = HabitFrequency.Daily };
        foreach (var d in isoDates)
            habit.CheckIn(DateOnly.Parse(d));
        return habit;
    }

    [Fact]
    public void CurrentStreak_Counts_Consecutive_Days_Ending_Today()
    {
        var habit = DailyHabit("2026-10-12", "2026-10-13", "2026-10-14");

        var report = StreakCalculator.Analyze(habit, Today);

        Assert.Equal(3, report.CurrentStreak);
        Assert.Equal(3, report.LongestStreak);
    }

    [Fact]
    public void CurrentStreak_Stays_Alive_When_Today_Not_Done_Yet()
    {
        var habit = DailyHabit("2026-10-13");

        var report = StreakCalculator.Analyze(habit, Today);

        Assert.Equal(1, report.CurrentStreak);
    }

    [Fact]
    public void CurrentStreak_Resets_After_A_Missed_Day()
    {
        var habit = DailyHabit("2026-10-10", "2026-10-14");

        var report = StreakCalculator.Analyze(habit, Today);

        Assert.Equal(1, report.CurrentStreak);
        Assert.Equal(1, report.LongestStreak);
    }

    [Fact]
    public void LongestStreak_Finds_Best_Run_Across_History()
    {
        var habit = DailyHabit(
            "2026-10-01", "2026-10-02", "2026-10-03", // run of 3
            "2026-10-10", "2026-10-11");              // run of 2

        var report = StreakCalculator.Analyze(habit, Today);

        Assert.Equal(3, report.LongestStreak);
        Assert.Equal(0, report.CurrentStreak);
    }

    [Fact]
    public void Duplicate_CheckIns_On_Same_Day_Count_Once()
    {
        var habit = new Habit { Name = "test" };
        Assert.True(habit.CheckIn(Today));
        Assert.False(habit.CheckIn(Today)); // idempotent

        var report = StreakCalculator.Analyze(habit, Today);

        Assert.Equal(1, report.CurrentStreak);
        Assert.Equal(1, report.CompletedPeriods);
    }

    [Fact]
    public void Empty_Habit_Returns_Zero_Report()
    {
        var report = StreakCalculator.Analyze(new Habit { Name = "test" }, Today);

        Assert.Equal(0, report.CurrentStreak);
        Assert.Equal(0, report.LongestStreak);
        Assert.Equal(0, report.CompletionRate);
    }

    [Fact]
    public void Weekly_Streak_Counts_Consecutive_Weeks()
    {
        var habit = new Habit { Name = "gym", Frequency = HabitFrequency.Weekly };
        habit.CheckIn(new DateOnly(2026, 9, 28));  // Monday, week 40
        habit.CheckIn(new DateOnly(2026, 10, 5));  // Monday, week 41
        habit.CheckIn(new DateOnly(2026, 10, 14)); // Wednesday, week 42

        var report = StreakCalculator.Analyze(habit, Today);

        Assert.Equal(3, report.CurrentStreak);
    }

    [Fact]
    public void Weekly_Streak_Breaks_On_Missed_Week()
    {
        var habit = new Habit { Name = "gym", Frequency = HabitFrequency.Weekly };
        habit.CheckIn(new DateOnly(2026, 9, 28));  // week 40
        habit.CheckIn(new DateOnly(2026, 10, 14)); // week 42 - week 41 missed

        var report = StreakCalculator.Analyze(habit, Today);

        Assert.Equal(1, report.CurrentStreak);
        Assert.Equal(1, report.LongestStreak);
    }

    [Fact]
    public void IsDoneFor_Respects_Weekly_Boundaries()
    {
        var habit = new Habit { Name = "gym", Frequency = HabitFrequency.Weekly };
        habit.CheckIn(new DateOnly(2026, 10, 12)); // Monday of this week

        Assert.True(StreakCalculator.IsDoneFor(habit, Today));
        Assert.False(StreakCalculator.IsDoneFor(habit, new DateOnly(2026, 10, 11))); // previous Sunday
    }
}
