using UnAccess.Core.Enums;

namespace UnAccess.Core.Models;

public sealed class AccessDecision
{
    public bool IsAllowed { get; init; }
    public string Reason { get; init; } = "";
    public int PersonnelId { get; init; }
    public int DoorId { get; init; }
    public MatchedRuleType MatchedRuleType { get; init; }
    public int? MatchedRuleId { get; init; }
    public int? TimeScheduleId { get; init; }
    public int? CardId { get; init; }
    public string CardNumber { get; init; } = "";
    public string PersonnelName { get; init; } = "";
    public string DoorName { get; init; } = "";
    public string ZoneName { get; init; } = "";
    public string Location { get; init; } = "";
    public int? ControllerId { get; init; }
    public int ChannelNumber { get; init; }
    public string SourceName { get; init; } = "";
    public string ResultText => IsAllowed ? "İZİN" : "YASAK";
}

public sealed class AccessGroupEditModel
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AccessGroupListItem
{
    public int Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public bool IsActive { get; init; }
}

public sealed class AccessTreeNode
{
    public string Title { get; init; } = "";
    public string Kind { get; init; } = "";
    public int? ZoneId { get; init; }
    public int? DoorId { get; init; }
    public int? RuleId { get; init; }
    public bool? IsAllowed { get; init; }
    public int? TimeScheduleId { get; init; }
    public string? ScheduleName { get; init; }
    public IReadOnlyList<AccessTreeNode> Children { get; init; } = [];
}

public sealed class TimeIntervalModel
{
    public int Id { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

public sealed class TimeScheduleEditModel
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public IReadOnlyList<TimeIntervalModel> Intervals { get; set; } = [];
}

public sealed class TimeScheduleListItem
{
    public int Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public bool IsActive { get; init; }
}

public sealed class PersonnelGroupItem
{
    public int Id { get; init; }
    public int AccessGroupId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public bool IsActive { get; init; }
}

public sealed class PersonnelGroupEditModel
{
    public int PersonnelId { get; set; }
    public int AccessGroupId { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PersonnelOverrideItem
{
    public int Id { get; init; }
    public int? ZoneId { get; init; }
    public int? DoorId { get; init; }
    public string TargetName { get; init; } = "";
    public int TimeScheduleId { get; init; }
    public string ScheduleName { get; init; } = "";
    public bool IsAllowed { get; init; }
    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public string? Reason { get; init; }
    public bool IsActive { get; init; }
    public string ResultText => IsAllowed ? "İZİN" : "YASAK";
}

public sealed class PersonnelOverrideEditModel
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
}

public sealed class PersonnelAuthorizationView
{
    public IReadOnlyList<PersonnelGroupItem> Groups { get; init; } = [];
    public IReadOnlyList<PersonnelOverrideItem> Overrides { get; init; } = [];
    public IReadOnlyList<AccessDecision> Effective { get; init; } = [];
}

public sealed class SimulatedPassageResult
{
    public required AccessDecision Decision { get; init; }
    public required string EventId { get; init; }
}

public sealed class AccessEventListItem
{
    public int Id { get; init; }
    public string TimeText { get; init; } = "";
    public string PersonnelName { get; init; } = "";
    public string CardNumber { get; init; } = "";
    public string DoorName { get; init; } = "";
    public string ZoneName { get; init; } = "";
    public string ResultText { get; init; } = "";
    public string Reason { get; init; } = "";
    public bool IsGranted { get; init; }
}
