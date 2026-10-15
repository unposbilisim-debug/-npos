using Microsoft.EntityFrameworkCore;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Access;
using UnAccess.Core.Enums;
using UnAccess.Core.Models;

namespace UnAccess.Data.Services;

internal sealed class AccessDecisionService : IAccessDecisionService
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly IHospitalClock _clock;
    private readonly ITimeScheduleEvaluator _schedules;

    public AccessDecisionService(
        IDbContextFactory<UnAccessDbContext> factory,
        IHospitalClock clock,
        ITimeScheduleEvaluator schedules)
    {
        _factory = factory;
        _clock = clock;
        _schedules = schedules;
    }

    public async Task<AccessDecision> EvaluateAsync(
        int personnelId,
        int doorId,
        DateTime evaluationTime,
        CancellationToken cancellationToken)
    {
        var decisions = await EvaluateDoorsAsync(personnelId, doorId, evaluationTime, cancellationToken);
        return decisions.SingleOrDefault() ?? new AccessDecision
        {
            IsAllowed = false,
            Reason = "Kapı bulunamadı.",
            PersonnelId = personnelId,
            DoorId = doorId,
            MatchedRuleType = MatchedRuleType.None
        };
    }

    public Task<IReadOnlyList<AccessDecision>> GetEffectiveAccessAsync(
        int personnelId,
        DateTime evaluationTime,
        CancellationToken cancellationToken)
    {
        return EvaluateDoorsAsync(personnelId, doorId: null, evaluationTime, cancellationToken);
    }

    private async Task<IReadOnlyList<AccessDecision>> EvaluateDoorsAsync(
        int personnelId,
        int? doorId,
        DateTime evaluationTime,
        CancellationToken cancellationToken)
    {
        var local = _clock.ToHospitalLocal(evaluationTime);
        var date = DateOnly.FromDateTime(local);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var doorsQuery =
            from door in db.Doors.AsNoTracking()
            join zone in db.Zones.AsNoTracking() on door.ZoneId equals zone.Id
            join floor in db.Floors.AsNoTracking() on zone.FloorId equals floor.Id
            join building in db.Buildings.AsNoTracking() on floor.BuildingId equals building.Id
            select new DoorRow
            {
                Id = door.Id,
                Name = door.Name,
                IsActive = door.IsActive,
                ZoneId = zone.Id,
                ZoneName = zone.Name,
                ZoneIsActive = zone.IsActive,
                FloorIsActive = floor.IsActive,
                BuildingName = building.Name,
                FloorName = floor.Name,
                BuildingIsActive = building.IsActive,
                ControllerId = door.ControllerId,
                ChannelNumber = door.ChannelNumber
            };
        if (doorId is int selectedDoor)
            doorsQuery = doorsQuery.Where(door => door.Id == selectedDoor);

        var doors = await doorsQuery.OrderBy(door => door.BuildingName).ThenBy(door => door.FloorName).ThenBy(door => door.Name).ToListAsync(cancellationToken);
        var person = await db.Personnel.AsNoTracking()
            .Where(item => item.Id == personnelId)
            .Select(item => new PersonRow
            {
                Id = item.Id,
                IsActive = item.IsActive,
                Name = item.FirstName + " " + item.LastName
            })
            .FirstOrDefaultAsync(cancellationToken);
        var cards = person == null
            ? []
            : await db.Cards.AsNoTracking()
                .Where(card => card.PersonnelId == personnelId && card.Status == CardStatus.Active)
                .Select(card => new AccessCardSnapshot
                {
                    Id = card.Id,
                    CardNumber = card.CardNumber,
                    ValidFrom = card.ValidFrom,
                    ValidUntil = card.ValidUntil
                })
                .ToListAsync(cancellationToken);

        var memberships = await (
            from membership in db.PersonnelAccessGroups.AsNoTracking()
            join groupItem in db.AccessGroups.AsNoTracking() on membership.AccessGroupId equals groupItem.Id
            where membership.PersonnelId == personnelId
                && membership.IsActive
                && groupItem.IsActive
                && membership.ValidFrom <= date
                && (membership.ValidUntil == null || membership.ValidUntil >= date)
            select new MembershipRow
            {
                AccessGroupId = groupItem.Id
            }).ToListAsync(cancellationToken);

        var groupIds = memberships.Select(item => item.AccessGroupId).Distinct().ToList();
        var zoneIds = doors.Select(door => door.ZoneId).Distinct().ToList();
        var doorIds = doors.Select(door => door.Id).ToList();

        var overrides = await db.PersonnelAccessOverrides.AsNoTracking()
            .Where(item => item.PersonnelId == personnelId
                && item.IsActive
                && item.ValidFrom <= date
                && (item.ValidUntil == null || item.ValidUntil >= date)
                && ((item.DoorId != null && doorIds.Contains(item.DoorId.Value))
                    || (item.ZoneId != null && zoneIds.Contains(item.ZoneId.Value))))
            .Select(item => new OverrideRow
            {
                Id = item.Id,
                DoorId = item.DoorId,
                ZoneId = item.ZoneId,
                IsAllowed = item.IsAllowed,
                TimeScheduleId = item.TimeScheduleId
            })
            .ToListAsync(cancellationToken);

        var doorRules = groupIds.Count == 0
            ? []
            : await (
                from rule in db.AccessGroupDoors.AsNoTracking()
                join groupItem in db.AccessGroups.AsNoTracking() on rule.AccessGroupId equals groupItem.Id
                where rule.IsActive && groupIds.Contains(rule.AccessGroupId) && doorIds.Contains(rule.DoorId)
                select new GroupRuleRow
                {
                    Id = rule.Id,
                    AccessGroupId = rule.AccessGroupId,
                    TargetId = rule.DoorId,
                    IsAllowed = rule.IsAllowed,
                    TimeScheduleId = rule.TimeScheduleId,
                    Code = groupItem.Code
                }).ToListAsync(cancellationToken);

        var zoneRules = groupIds.Count == 0
            ? []
            : await (
                from rule in db.AccessGroupZones.AsNoTracking()
                join groupItem in db.AccessGroups.AsNoTracking() on rule.AccessGroupId equals groupItem.Id
                where rule.IsActive && groupIds.Contains(rule.AccessGroupId) && zoneIds.Contains(rule.ZoneId)
                select new GroupRuleRow
                {
                    Id = rule.Id,
                    AccessGroupId = rule.AccessGroupId,
                    TargetId = rule.ZoneId,
                    IsAllowed = rule.IsAllowed,
                    TimeScheduleId = rule.TimeScheduleId,
                    Code = groupItem.Code
                }).ToListAsync(cancellationToken);

        var scheduleIds = overrides.Select(item => item.TimeScheduleId)
            .Concat(doorRules.Select(item => item.TimeScheduleId))
            .Concat(zoneRules.Select(item => item.TimeScheduleId))
            .Distinct()
            .ToList();
        var scheduleRows = scheduleIds.Count == 0
            ? []
            : await db.TimeSchedules.AsNoTracking()
                .Where(item => scheduleIds.Contains(item.Id))
                .Select(item => new ScheduleRow { Id = item.Id, IsActive = item.IsActive })
                .ToListAsync(cancellationToken);
        var detailRows = scheduleIds.Count == 0
            ? []
            : await db.TimeScheduleDetails.AsNoTracking()
                .Where(item => scheduleIds.Contains(item.TimeScheduleId) && item.IsActive)
                .Select(item => new DetailRow
                {
                    TimeScheduleId = item.TimeScheduleId,
                    DayOfWeek = item.DayOfWeek,
                    StartTime = item.StartTime,
                    EndTime = item.EndTime
                })
                .ToListAsync(cancellationToken);

        var windows = detailRows
            .GroupBy(item => item.TimeScheduleId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TimeWindow>)group
                    .Select(item => new TimeWindow(item.DayOfWeek, item.StartTime, item.EndTime))
                    .ToList());
        var scheduleActive = scheduleRows.ToDictionary(item => item.Id, item => item.IsActive);

        var decisions = new List<AccessDecision>(doors.Count);
        foreach (var door in doors)
        {
            var snapshot = new AccessSnapshot
            {
                PersonnelId = personnelId,
                DoorId = door.Id,
                PersonnelExists = person != null,
                PersonnelIsActive = person?.IsActive ?? false,
                PersonnelName = person?.Name ?? "",
                ActiveCards = cards,
                DoorExists = true,
                DoorIsActive = door.IsActive,
                ZoneIsActive = door.ZoneIsActive,
                FloorIsActive = door.FloorIsActive,
                BuildingIsActive = door.BuildingIsActive,
                DoorName = door.Name,
                ZoneName = door.ZoneName,
                Location = $"{door.BuildingName} / {door.FloorName} / {door.ZoneName}",
                ControllerId = door.ControllerId,
                ChannelNumber = door.ChannelNumber,
                PersonnelDoorOverrides = overrides
                    .Where(item => item.DoorId == door.Id)
                    .Select(item => ToRule(item.Id, item.IsAllowed, item.TimeScheduleId, "KİŞİSEL İSTİSNA", scheduleActive, windows))
                    .ToList(),
                PersonnelZoneOverrides = overrides
                    .Where(item => item.ZoneId == door.ZoneId)
                    .Select(item => ToRule(item.Id, item.IsAllowed, item.TimeScheduleId, "KİŞİSEL İSTİSNA", scheduleActive, windows))
                    .ToList(),
                GroupDoorRules = doorRules
                    .Where(item => item.TargetId == door.Id)
                    .Select(item => ToRule(item.Id, item.IsAllowed, item.TimeScheduleId, item.Code, scheduleActive, windows))
                    .ToList(),
                GroupZoneRules = zoneRules
                    .Where(item => item.TargetId == door.ZoneId)
                    .Select(item => ToRule(item.Id, item.IsAllowed, item.TimeScheduleId, item.Code, scheduleActive, windows))
                    .ToList()
            };
            decisions.Add(AccessRuleResolver.Resolve(snapshot, local, _schedules));
        }

        return decisions;
    }

    private static AccessRuleSnapshot ToRule(
        int id,
        bool isAllowed,
        int scheduleId,
        string sourceCode,
        IReadOnlyDictionary<int, bool> scheduleActive,
        IReadOnlyDictionary<int, IReadOnlyList<TimeWindow>> windows)
    {
        return new AccessRuleSnapshot
        {
            RuleId = id,
            IsAllowed = isAllowed,
            TimeScheduleId = scheduleId,
            ScheduleIsActive = scheduleActive.TryGetValue(scheduleId, out var active) && active,
            SourceCode = sourceCode,
            Windows = windows.TryGetValue(scheduleId, out var items) ? items : []
        };
    }

    private sealed class DoorRow
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public bool IsActive { get; init; }
        public int ZoneId { get; init; }
        public string ZoneName { get; init; } = "";
        public bool ZoneIsActive { get; init; }
        public bool FloorIsActive { get; init; }
        public bool BuildingIsActive { get; init; }
        public string BuildingName { get; init; } = "";
        public string FloorName { get; init; } = "";
        public int? ControllerId { get; init; }
        public int ChannelNumber { get; init; }
    }

    private sealed class PersonRow
    {
        public int Id { get; init; }
        public bool IsActive { get; init; }
        public string Name { get; init; } = "";
    }

    private sealed class MembershipRow
    {
        public int AccessGroupId { get; init; }
    }

    private sealed class OverrideRow
    {
        public int Id { get; init; }
        public int? DoorId { get; init; }
        public int? ZoneId { get; init; }
        public bool IsAllowed { get; init; }
        public int TimeScheduleId { get; init; }
    }

    private sealed class GroupRuleRow
    {
        public int Id { get; init; }
        public int AccessGroupId { get; init; }
        public int TargetId { get; init; }
        public bool IsAllowed { get; init; }
        public int TimeScheduleId { get; init; }
        public string Code { get; init; } = "";
    }

    private sealed class ScheduleRow
    {
        public int Id { get; init; }
        public bool IsActive { get; init; }
    }

    private sealed class DetailRow
    {
        public int TimeScheduleId { get; init; }
        public DayOfWeek DayOfWeek { get; init; }
        public TimeOnly StartTime { get; init; }
        public TimeOnly EndTime { get; init; }
    }
}
