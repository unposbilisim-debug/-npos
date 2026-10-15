namespace UnAccess.Core.Entities;

public sealed class TimeScheduleDetail
{
    public int Id { get; set; }
    public int TimeScheduleId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsActive { get; set; } = true;

    public TimeSchedule TimeSchedule { get; set; } = null!;
}
