using UnAccess.Core.Abstractions;
using UnAccess.Core.Enums;
using UnAccess.Core.Models;
using UnAccess.Core.Security;

namespace UnAccess.Data.Services;

internal sealed class GuardedPersonnelService : IPersonnelService
{
    private readonly PersonnelService _inner;
    private readonly IAccessGuard _guard;

    public GuardedPersonnelService(PersonnelService inner, IAccessGuard guard)
    {
        _inner = inner;
        _guard = guard;
    }

    public Task<IReadOnlyList<PersonnelListItem>> SearchAsync(PersonnelSearchCriteria criteria, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.PersonnelView);
        return _inner.SearchAsync(criteria, cancellationToken);
    }

    public Task<PersonnelDetail?> GetAsync(int id, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.PersonnelView);
        return _inner.GetAsync(id, cancellationToken);
    }

    public Task<PersonnelDetail> CreateAsync(PersonnelEditModel model, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.PersonnelManage);
        return _inner.CreateAsync(model, cancellationToken);
    }

    public Task<PersonnelDetail> UpdateAsync(int id, PersonnelEditModel model, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.PersonnelManage);
        return _inner.UpdateAsync(id, model, cancellationToken);
    }

    public Task<IReadOnlyList<LookupItem>> GetDepartmentsAsync(CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.PersonnelView);
        return _inner.GetDepartmentsAsync(cancellationToken);
    }

    public Task<IReadOnlyList<LookupItem>> GetJobTitlesAsync(CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.PersonnelView);
        return _inner.GetJobTitlesAsync(cancellationToken);
    }
}

internal sealed class GuardedCardService : ICardService
{
    private readonly CardService _inner;
    private readonly IAccessGuard _guard;

    public GuardedCardService(CardService inner, IAccessGuard guard)
    {
        _inner = inner;
        _guard = guard;
    }

    public Task<IReadOnlyList<CardListItem>> SearchAsync(CardSearchCriteria criteria, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.CardsView);
        return _inner.SearchAsync(criteria, cancellationToken);
    }

    public Task<IReadOnlyList<CardListItem>> GetByPersonnelAsync(int personnelId, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.CardsView);
        return _inner.GetByPersonnelAsync(personnelId, cancellationToken);
    }

    public Task<CardListItem> CreateAsync(CardEditModel model, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.CardsManage);
        return _inner.CreateAsync(model, cancellationToken);
    }

    public Task<CardListItem> SetStatusAsync(int id, CardStatus status, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.CardsManage);
        return _inner.SetStatusAsync(id, status, cancellationToken);
    }

    public Task<IReadOnlyList<PersonnelOption>> GetPersonnelOptionsAsync(CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.CardsView);
        return _inner.GetPersonnelOptionsAsync(cancellationToken);
    }
}

internal sealed class GuardedHospitalStructureService : IHospitalStructureService
{
    private readonly HospitalStructureService _inner;
    private readonly IAccessGuard _guard;

    public GuardedHospitalStructureService(HospitalStructureService inner, IAccessGuard guard)
    {
        _inner = inner;
        _guard = guard;
    }

    public Task<IReadOnlyList<BuildingListItem>> ListBuildingsAsync(CancellationToken cancellationToken) => View(() => _inner.ListBuildingsAsync(cancellationToken));
    public Task<BuildingListItem?> GetBuildingAsync(int id, CancellationToken cancellationToken) => View(() => _inner.GetBuildingAsync(id, cancellationToken));
    public Task<BuildingListItem> CreateBuildingAsync(BuildingEditModel model, CancellationToken cancellationToken) => Manage(() => _inner.CreateBuildingAsync(model, cancellationToken));
    public Task<BuildingListItem> UpdateBuildingAsync(int id, BuildingEditModel model, CancellationToken cancellationToken) => Manage(() => _inner.UpdateBuildingAsync(id, model, cancellationToken));
    public Task<IReadOnlyList<FloorListItem>> ListFloorsAsync(int? buildingId, CancellationToken cancellationToken) => View(() => _inner.ListFloorsAsync(buildingId, cancellationToken));
    public Task<FloorListItem?> GetFloorAsync(int id, CancellationToken cancellationToken) => View(() => _inner.GetFloorAsync(id, cancellationToken));
    public Task<FloorListItem> CreateFloorAsync(FloorEditModel model, CancellationToken cancellationToken) => Manage(() => _inner.CreateFloorAsync(model, cancellationToken));
    public Task<FloorListItem> UpdateFloorAsync(int id, FloorEditModel model, CancellationToken cancellationToken) => Manage(() => _inner.UpdateFloorAsync(id, model, cancellationToken));
    public Task<IReadOnlyList<ZoneListItem>> ListZonesAsync(int? buildingId, int? floorId, CancellationToken cancellationToken) => View(() => _inner.ListZonesAsync(buildingId, floorId, cancellationToken));
    public Task<ZoneListItem?> GetZoneAsync(int id, CancellationToken cancellationToken) => View(() => _inner.GetZoneAsync(id, cancellationToken));
    public Task<ZoneListItem> CreateZoneAsync(ZoneEditModel model, CancellationToken cancellationToken) => Manage(() => _inner.CreateZoneAsync(model, cancellationToken));
    public Task<ZoneListItem> UpdateZoneAsync(int id, ZoneEditModel model, CancellationToken cancellationToken) => Manage(() => _inner.UpdateZoneAsync(id, model, cancellationToken));
    public Task<IReadOnlyList<DoorListItem>> ListDoorsAsync(int? zoneId, CancellationToken cancellationToken) => View(() => _inner.ListDoorsAsync(zoneId, cancellationToken));
    public Task<DoorEditModel?> GetDoorAsync(int id, CancellationToken cancellationToken) => View(() => _inner.GetDoorAsync(id, cancellationToken));
    public Task<DoorListItem> CreateDoorAsync(DoorEditModel model, CancellationToken cancellationToken) => Manage(() => _inner.CreateDoorAsync(model, cancellationToken));
    public Task<DoorListItem> UpdateDoorAsync(int id, DoorEditModel model, CancellationToken cancellationToken) => Manage(() => _inner.UpdateDoorAsync(id, model, cancellationToken));
    public Task<IReadOnlyList<LookupItem>> GetBuildingOptionsAsync(CancellationToken cancellationToken) => View(() => _inner.GetBuildingOptionsAsync(cancellationToken));
    public Task<IReadOnlyList<FloorOption>> GetFloorOptionsAsync(int? buildingId, CancellationToken cancellationToken) => View(() => _inner.GetFloorOptionsAsync(buildingId, cancellationToken));
    public Task<IReadOnlyList<ZoneOption>> GetZoneOptionsAsync(CancellationToken cancellationToken) => View(() => _inner.GetZoneOptionsAsync(cancellationToken));
    public Task<IReadOnlyList<ControllerOption>> GetControllerOptionsAsync(CancellationToken cancellationToken) => View(() => _inner.GetControllerOptionsAsync(cancellationToken));

    private Task<T> View<T>(Func<Task<T>> action)
    {
        _guard.Require(Permissions.HospitalView);
        return action();
    }

    private Task<T> Manage<T>(Func<Task<T>> action)
    {
        _guard.Require(Permissions.HospitalManage);
        return action();
    }
}

internal sealed class GuardedAccessGroupService : IAccessGroupService
{
    private readonly AccessGroupService _inner;
    private readonly IAccessGuard _guard;

    public GuardedAccessGroupService(AccessGroupService inner, IAccessGuard guard)
    {
        _inner = inner;
        _guard = guard;
    }

    public Task<IReadOnlyList<AccessGroupListItem>> ListAsync(CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.AccessView);
        return _inner.ListAsync(cancellationToken);
    }

    public Task<AccessGroupListItem> SaveAsync(int? id, AccessGroupEditModel model, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.AccessManage);
        return _inner.SaveAsync(id, model, cancellationToken);
    }

    public Task<IReadOnlyList<AccessTreeNode>> GetTreeAsync(int accessGroupId, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.AccessView);
        return _inner.GetTreeAsync(accessGroupId, cancellationToken);
    }

    public Task SetZoneRuleAsync(int accessGroupId, int zoneId, int timeScheduleId, bool isAllowed, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.AccessManage);
        return _inner.SetZoneRuleAsync(accessGroupId, zoneId, timeScheduleId, isAllowed, cancellationToken);
    }

    public Task ClearZoneRuleAsync(int accessGroupId, int zoneId, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.AccessManage);
        return _inner.ClearZoneRuleAsync(accessGroupId, zoneId, cancellationToken);
    }

    public Task SetDoorRuleAsync(int accessGroupId, int doorId, int timeScheduleId, bool isAllowed, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.AccessManage);
        return _inner.SetDoorRuleAsync(accessGroupId, doorId, timeScheduleId, isAllowed, cancellationToken);
    }

    public Task ClearDoorRuleAsync(int accessGroupId, int doorId, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.AccessManage);
        return _inner.ClearDoorRuleAsync(accessGroupId, doorId, cancellationToken);
    }
}

internal sealed class GuardedTimeScheduleService : ITimeScheduleService
{
    private readonly TimeScheduleService _inner;
    private readonly IAccessGuard _guard;

    public GuardedTimeScheduleService(TimeScheduleService inner, IAccessGuard guard)
    {
        _inner = inner;
        _guard = guard;
    }

    public Task<IReadOnlyList<TimeScheduleListItem>> ListAsync(CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.AccessView);
        return _inner.ListAsync(cancellationToken);
    }

    public Task<TimeScheduleEditModel?> GetAsync(int id, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.AccessView);
        return _inner.GetAsync(id, cancellationToken);
    }

    public Task<TimeScheduleListItem> SaveAsync(TimeScheduleEditModel model, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.AccessManage);
        return _inner.SaveAsync(model, cancellationToken);
    }
}

internal sealed class GuardedPersonnelAuthorizationService : IPersonnelAuthorizationService
{
    private readonly PersonnelAuthorizationService _inner;
    private readonly IAccessGuard _guard;

    public GuardedPersonnelAuthorizationService(PersonnelAuthorizationService inner, IAccessGuard guard)
    {
        _inner = inner;
        _guard = guard;
    }

    public Task<PersonnelAuthorizationView> GetAsync(int personnelId, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.AccessView);
        return _inner.GetAsync(personnelId, cancellationToken);
    }

    public Task AssignGroupAsync(PersonnelGroupEditModel model, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.AccessManage);
        return _inner.AssignGroupAsync(model, cancellationToken);
    }

    public Task RemoveGroupAsync(int personnelId, int accessGroupId, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.AccessManage);
        return _inner.RemoveGroupAsync(personnelId, accessGroupId, cancellationToken);
    }

    public Task SaveOverrideAsync(PersonnelOverrideEditModel model, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.AccessManage);
        return _inner.SaveOverrideAsync(model, cancellationToken);
    }
}

internal sealed class GuardedControllerQueryService : IControllerQueryService
{
    private readonly ControllerQueryService _inner;
    private readonly IAccessGuard _guard;

    public GuardedControllerQueryService(ControllerQueryService inner, IAccessGuard guard)
    {
        _inner = inner;
        _guard = guard;
    }

    public Task<IReadOnlyList<ControllerListItem>> ListAsync(CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.DevicesView);
        return _inner.ListAsync(cancellationToken);
    }
}

internal sealed class GuardedDeviceSyncService : IDeviceSyncService
{
    private readonly DeviceSyncService _inner;
    private readonly IAccessGuard _guard;

    public GuardedDeviceSyncService(DeviceSyncService inner, IAccessGuard guard)
    {
        _inner = inner;
        _guard = guard;
    }

    public Task<DeviceSyncSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.DevicesView);
        return _inner.GetSummaryAsync(cancellationToken);
    }

    public Task<IReadOnlyList<DeviceSyncJobItem>> SearchAsync(DeviceSyncFilter filter, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.DevicesView);
        return _inner.SearchAsync(filter, cancellationToken);
    }

    public Task<int> RetryFailedAsync(CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.DevicesOperate);
        return _inner.RetryFailedAsync(cancellationToken);
    }

    public Task RequestFullSyncAsync(int controllerId, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.DevicesOperate);
        return _inner.RequestFullSyncAsync(controllerId, cancellationToken);
    }
}

internal sealed class GuardedAccessDecisionService : IAccessDecisionService
{
    private readonly AccessDecisionService _inner;
    private readonly IAccessGuard _guard;

    public GuardedAccessDecisionService(AccessDecisionService inner, IAccessGuard guard)
    {
        _inner = inner;
        _guard = guard;
    }

    public Task<AccessDecision> EvaluateAsync(int personnelId, int doorId, DateTime evaluationTime, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.AccessView);
        return _inner.EvaluateAsync(personnelId, doorId, evaluationTime, cancellationToken);
    }

    public Task<IReadOnlyList<AccessDecision>> GetEffectiveAccessAsync(int personnelId, DateTime evaluationTime, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.AccessView);
        return _inner.GetEffectiveAccessAsync(personnelId, evaluationTime, cancellationToken);
    }
}

internal sealed class GuardedPassageRecorder : IPassageRecorder
{
    private readonly PassageRecorder _inner;
    private readonly IAccessGuard _guard;

    public GuardedPassageRecorder(PassageRecorder inner, IAccessGuard guard)
    {
        _inner = inner;
        _guard = guard;
    }

    public Task<SimulatedPassageResult> RecordAsync(int personnelId, int doorId, DateTime hospitalLocal, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.PassageSimulate);
        return _inner.RecordAsync(personnelId, doorId, hospitalLocal, cancellationToken);
    }
}

internal sealed class GuardedAccessEventQueryService : IAccessEventQueryService
{
    private readonly AccessEventQueryService _inner;
    private readonly IAccessGuard _guard;

    public GuardedAccessEventQueryService(AccessEventQueryService inner, IAccessGuard guard)
    {
        _inner = inner;
        _guard = guard;
    }

    public Task<IReadOnlyList<AccessEventListItem>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.LiveView);
        return _inner.GetRecentAsync(take, cancellationToken);
    }
}
