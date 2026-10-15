namespace UnAccess.Core.Entities;

public sealed class AccessGroupZone
{
    public int Id { get; set; }
    public int AccessGroupId { get; set; }
    public int ZoneId { get; set; }
    public int TimeScheduleId { get; set; }
    public bool IsAllowed { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public AccessGroup AccessGroup { get; set; } = null!;
    public Zone Zone { get; set; } = null!;
    public TimeSchedule TimeSchedule { get; set; } = null!;
}
