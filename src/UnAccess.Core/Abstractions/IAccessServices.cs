using UnAccess.Core.Models;

namespace UnAccess.Core.Abstractions;

public interface IHospitalClock
{
    TimeZoneInfo Zone { get; }
    DateTime ToHospitalLocal(DateTime evaluationTime);
    DateTime ToUtc(DateTime hospitalLocal);
    void ApplyTimeZone(string timeZoneId);
}

public interface IAccessDecisionService
{
    Task<AccessDecision> EvaluateAsync(int personnelId, int doorId, DateTime evaluationTime, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccessDecision>> GetEffectiveAccessAsync(int personnelId, DateTime evaluationTime, CancellationToken cancellationToken);
}

public interface IAccessGroupService
{
    Task<IReadOnlyList<AccessGroupListItem>> ListAsync(CancellationToken cancellationToken);
    Task<AccessGroupListItem> SaveAsync(int? id, AccessGroupEditModel model, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccessTreeNode>> GetTreeAsync(int accessGroupId, CancellationToken cancellationToken);
    Task SetZoneRuleAsync(int accessGroupId, int zoneId, int timeScheduleId, bool isAllowed, CancellationToken cancellationToken);
    Task ClearZoneRuleAsync(int accessGroupId, int zoneId, CancellationToken cancellationToken);
    Task SetDoorRuleAsync(int accessGroupId, int doorId, int timeScheduleId, bool isAllowed, CancellationToken cancellationToken);
    Task ClearDoorRuleAsync(int accessGroupId, int doorId, CancellationToken cancellationToken);
}

public interface ITimeScheduleService
{
    Task<IReadOnlyList<TimeScheduleListItem>> ListAsync(CancellationToken cancellationToken);
    Task<TimeScheduleEditModel?> GetAsync(int id, CancellationToken cancellationToken);
    Task<TimeScheduleListItem> SaveAsync(TimeScheduleEditModel model, CancellationToken cancellationToken);
}

public interface IPersonnelAuthorizationService
{
    Task<PersonnelAuthorizationView> GetAsync(int personnelId, CancellationToken cancellationToken);
    Task AssignGroupAsync(PersonnelGroupEditModel model, CancellationToken cancellationToken);
    Task RemoveGroupAsync(int personnelId, int accessGroupId, CancellationToken cancellationToken);
    Task SaveOverrideAsync(PersonnelOverrideEditModel model, CancellationToken cancellationToken);
}

public interface IPassageRecorder
{
    Task<SimulatedPassageResult> RecordAsync(int personnelId, int doorId, DateTime hospitalLocal, CancellationToken cancellationToken);
}

public interface IAccessEventQueryService
{
    Task<IReadOnlyList<AccessEventListItem>> GetRecentAsync(int take, CancellationToken cancellationToken);
}

public interface ISimulatedPassageService
{
    Task<SimulatedPassageResult> ReadCardAsync(int personnelId, int doorId, DateTime hospitalLocal, CancellationToken cancellationToken);
}
