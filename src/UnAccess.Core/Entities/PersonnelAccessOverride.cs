using UnAccess.Core.Common;

namespace UnAccess.Core.Entities;

public sealed class PersonnelAccessOverride : IHasTimestamps
{
    public int Id { get; set; }
    public int PersonnelId { get; set; }
    public int? ZoneId { get; set; }
    public int? DoorId { get; set; }
    public int TimeScheduleId { get; set; }
    public bool IsAllowed { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public string? Reason { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Personnel Personnel { get; set; } = null!;
    public Zone? Zone { get; set; }
    public Door? Door { get; set; }
    public TimeSchedule TimeSchedule { get; set; } = null!;
}
