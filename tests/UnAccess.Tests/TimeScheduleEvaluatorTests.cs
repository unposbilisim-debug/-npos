using UnAccess.Core.Access;

namespace UnAccess.Tests;

public class TimeScheduleEvaluatorTests
{
    private readonly TimeScheduleEvaluator _evaluator = new();

    [Fact]
    public void Overnight_window_allows_late_evening_and_next_morning()
    {
        var windows = new[] { new TimeWindow(DayOfWeek.Monday, new TimeOnly(22, 0), new TimeOnly(6, 0)) };

        Assert.True(Matches(windows, new DateTime(2026, 10, 12, 23, 0, 0)));
        Assert.True(Matches(windows, new DateTime(2026, 10, 13, 2, 0, 0)));
        Assert.False(Matches(windows, new DateTime(2026, 10, 12, 14, 0, 0)));
        Assert.False(Matches(windows, new DateTime(2026, 10, 13, 23, 0, 0)));
    }

    [Theory]
    [InlineData(23, 0, true)]
    [InlineData(2, 0, true)]
    [InlineData(14, 0, false)]
    public void Daily_night_plan_covers_both_sides_of_midnight(int hour, int minute, bool expected)
    {
        var windows = Enum.GetValues<DayOfWeek>()
            .Select(day => new TimeWindow(day, new TimeOnly(22, 0), new TimeOnly(6, 0)))
            .ToList();
        var day = hour == 2 ? new DateTime(2026, 10, 16, hour, minute, 0) : new DateTime(2026, 10, 15, hour, minute, 0);
        Assert.Equal(expected, Matches(windows, day));
    }

    [Fact]
    public void Equal_start_and_end_cover_the_whole_day()
    {
        var windows = new[] { new TimeWindow(DayOfWeek.Thursday, new TimeOnly(0, 0), new TimeOnly(0, 0)) };
        Assert.True(Matches(windows, new DateTime(2026, 10, 15, 14, 30, 0)));
        Assert.False(Matches(windows, new DateTime(2026, 10, 16, 14, 30, 0)));
    }

    private bool Matches(IReadOnlyList<TimeWindow> windows, DateTime local) =>
        _evaluator.IsMatch(true, windows, local);
}
