namespace UnAccess.Core.Access;

public readonly record struct TimeWindow(DayOfWeek Day, TimeOnly Start, TimeOnly End);

public interface ITimeScheduleEvaluator
{
    bool IsMatch(bool scheduleIsActive, IReadOnlyList<TimeWindow> windows, DateTime hospitalLocal);
}

public sealed class TimeScheduleEvaluator : ITimeScheduleEvaluator
{
    public bool IsMatch(bool scheduleIsActive, IReadOnlyList<TimeWindow> windows, DateTime hospitalLocal)
    {
        if (!scheduleIsActive || windows.Count == 0)
            return false;

        var time = TimeOnly.FromDateTime(hospitalLocal);
        if (Contains(windows, hospitalLocal.DayOfWeek, time, spillFromPreviousDay: false))
            return true;

        var previous = hospitalLocal.AddDays(-1).DayOfWeek;
        return Contains(windows, previous, time, spillFromPreviousDay: true);
    }

    private static bool Contains(
        IReadOnlyList<TimeWindow> windows,
        DayOfWeek day,
        TimeOnly time,
        bool spillFromPreviousDay)
    {
        foreach (var window in windows)
        {
            if (window.Day != day)
                continue;

            if (window.Start == window.End)
            {
                if (!spillFromPreviousDay)
                    return true;
                continue;
            }

            if (window.Start < window.End)
            {
                if (!spillFromPreviousDay && time >= window.Start && time <= window.End)
                    return true;
                continue;
            }

            if (!spillFromPreviousDay && time >= window.Start)
                return true;
            if (spillFromPreviousDay && time <= window.End)
                return true;
        }

        return false;
    }
}
