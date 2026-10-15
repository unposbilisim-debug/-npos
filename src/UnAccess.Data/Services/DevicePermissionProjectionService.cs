using Microsoft.EntityFrameworkCore;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Access;
using UnAccess.Core.Enums;
using UnAccess.Core.Models;

namespace UnAccess.Data.Services;

internal sealed class DevicePermissionProjectionService : IDevicePermissionProjectionService
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly IHospitalClock _hospitalClock;
    private readonly IUtcClock _clock;

    public DevicePermissionProjectionService(
        IDbContextFactory<UnAccessDbContext> factory,
        IHospitalClock hospitalClock,
        IUtcClock clock)
    {
        _factory = factory;
        _hospitalClock = hospitalClock;
        _clock = clock;
    }

    public async Task<PersonnelDeviceProjection> ProjectPersonnelAsync(int personnelId, CancellationToken cancellationToken)
    {
        var rows = await ProjectManyAsync([personnelId], controllerId: null, cancellationToken);
        return rows.Single();
    }

    public async Task<IReadOnlyList<DeviceCardCommand>> ProjectControllerAsync(int controllerId, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var localDate = DateOnly.FromDateTime(_hospitalClock.ToHospitalLocal(_clock.UtcNow));
        var doors = await LoadDoorsAsync(db, controllerId, cancellationToken);
        if (doors.Count == 0)
            return [];

        var doorIds = doors.Select(door => door.Id).ToList();
        var zoneIds = doors.Select(door => door.ZoneId).Distinct().ToList();
        var personnelIds = await CandidatePersonnelAsync(db, doorIds, zoneIds, localDate, cancellationToken);
        if (personnelIds.Count == 0)
            return [];

        var projections = await ProjectManyAsync(personnelIds, controllerId, cancellationToken);
        return projections
            .Where(item => item.CardId != null && item.Controllers.Count > 0)
            .Select(item => new DeviceCardCommand
            {
                CardNumber = item.CardNumber,
                PersonnelNumber = item.PersonnelNumber,
                ValidFrom = item.ValidFrom,
                ValidUntil = item.ValidUntil,
                Channels = item.Controllers.SelectMany(controller => controller.Channels).Select(ToCommand).ToList()
            })
            .OrderBy(item => item.CardNumber, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<IReadOnlyList<PersonnelDeviceProjection>> ProjectManyAsync(
        IReadOnlyList<int> personnelIds,
        int? controllerId,
        CancellationToken cancellationToken)
    {
        var localDate = DateOnly.FromDateTime(_hospitalClock.ToHospitalLocal(_clock.UtcNow));
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var doors = await LoadDoorsAsync(db, controllerId, cancellationToken);
        var people = await db.Personnel.AsNoTracking()
            .Where(person => personnelIds.Contains(person.Id))
            .Select(person => new PersonRow
            {
                Id = person.Id,
                IsActive = person.IsActive,
                PersonnelNumber = person.PersonnelNumber
            })
            .ToListAsync(cancellationToken);
        var cards = await db.Cards.AsNoTracking()
            .Where(card => personnelIds.Contains(card.PersonnelId) && card.Status == CardStatus.Active)
            .Select(card => new CardRow
            {
                Id = card.Id,
                PersonnelId = card.PersonnelId,
                CardNumber = card.CardNumber,
                ValidFrom = card.ValidFrom,
                ValidUntil = card.ValidUntil
            })
            .ToListAsync(cancellationToken);

        var doorIds = doors.Select(door => door.Id).ToList();
        var zoneIds = doors.Select(door => door.ZoneId).Distinct().ToList();
        var memberships = await (
            from membership in db.PersonnelAccessGroups.AsNoTracking()
            join groupItem in db.AccessGroups.AsNoTracking() on membership.AccessGroupId equals groupItem.Id
            where personnelIds.Contains(membership.PersonnelId)
                && membership.IsActive
                && groupItem.IsActive
                && membership.ValidFrom <= localDate
                && (membership.ValidUntil == null || membership.ValidUntil >= localDate)
            select new MembershipRow
            {
                PersonnelId = membership.PersonnelId,
                AccessGroupId = groupItem.Id
            }).ToListAsync(cancellationToken);

        var groupIds = memberships.Select(item => item.AccessGroupId).Distinct().ToList();
        var doorRules = groupIds.Count == 0 || doorIds.Count == 0
            ? []
            : await (
                from rule in db.AccessGroupDoors.AsNoTracking()
                join groupItem in db.AccessGroups.AsNoTracking() on rule.AccessGroupId equals groupItem.Id
                where rule.IsActive && groupIds.Contains(rule.AccessGroupId) && doorIds.Contains(rule.DoorId)
                select new RuleRow
                {
                    Id = rule.Id,
                    AccessGroupId = rule.AccessGroupId,
                    TargetId = rule.DoorId,
                    IsAllowed = rule.IsAllowed,
                    TimeScheduleId = rule.TimeScheduleId,
                    Code = groupItem.Code
                }).ToListAsync(cancellationToken);
        var zoneRules = groupIds.Count == 0 || zoneIds.Count == 0
            ? []
            : await (
                from rule in db.AccessGroupZones.AsNoTracking()
                join groupItem in db.AccessGroups.AsNoTracking() on rule.AccessGroupId equals groupItem.Id
                where rule.IsActive && groupIds.Contains(rule.AccessGroupId) && zoneIds.Contains(rule.ZoneId)
                select new RuleRow
                {
                    Id = rule.Id,
                    AccessGroupId = rule.AccessGroupId,
                    TargetId = rule.ZoneId,
                    IsAllowed = rule.IsAllowed,
                    TimeScheduleId = rule.TimeScheduleId,
                    Code = groupItem.Code
                }).ToListAsync(cancellationToken);
        var overrides = doorIds.Count == 0 && zoneIds.Count == 0
            ? []
            : await db.PersonnelAccessOverrides.AsNoTracking()
                .Where(item => personnelIds.Contains(item.PersonnelId)
                    && item.IsActive
                    && item.ValidFrom <= localDate
                    && (item.ValidUntil == null || item.ValidUntil >= localDate)
                    && ((item.DoorId != null && doorIds.Contains(item.DoorId.Value))
                        || (item.ZoneId != null && zoneIds.Contains(item.ZoneId.Value))))
                .Select(item => new OverrideRow
                {
                    Id = item.Id,
                    PersonnelId = item.PersonnelId,
                    DoorId = item.DoorId,
                    ZoneId = item.ZoneId,
                    IsAllowed = item.IsAllowed,
                    TimeScheduleId = item.TimeScheduleId
                })
                .ToListAsync(cancellationToken);

        var scheduleIds = doorRules.Select(item => item.TimeScheduleId)
            .Concat(zoneRules.Select(item => item.TimeScheduleId))
            .Concat(overrides.Select(item => item.TimeScheduleId))
            .Distinct()
            .ToList();
        var schedules = scheduleIds.Count == 0
            ? []
            : await db.TimeSchedules.AsNoTracking()
                .Where(item => scheduleIds.Contains(item.Id))
                .Select(item => new ScheduleRow { Id = item.Id, Code = item.Code, IsActive = item.IsActive })
                .ToListAsync(cancellationToken);
        var details = scheduleIds.Count == 0
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
        var windows = details.GroupBy(item => item.TimeScheduleId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TimeWindow>)group.Select(item => new TimeWindow(item.DayOfWeek, item.StartTime, item.EndTime)).ToList());
        var scheduleActive = schedules.ToDictionary(item => item.Id, item => item.IsActive);
        var scheduleCodes = schedules.ToDictionary(item => item.Id, item => item.Code);

        var results = new List<PersonnelDeviceProjection>(personnelIds.Count);
        foreach (var personnelId in personnelIds.Distinct())
        {
            var person = people.FirstOrDefault(item => item.Id == personnelId);
            var card = cards
                .Where(item => item.PersonnelId == personnelId && item.ValidFrom <= localDate && (item.ValidUntil == null || item.ValidUntil >= localDate))
                .OrderBy(item => item.Id)
                .FirstOrDefault();
            if (person == null || !person.IsActive || card == null)
            {
                results.Add(new PersonnelDeviceProjection { PersonnelId = personnelId, PersonnelNumber = person?.PersonnelNumber ?? "" });
                continue;
            }

            var groupIdsForPerson = memberships.Where(item => item.PersonnelId == personnelId).Select(item => item.AccessGroupId).ToHashSet();
            var channels = new List<ChannelRow>();
            foreach (var door in doors)
            {
                var snapshot = new AccessSnapshot
                {
                    PersonnelId = personnelId,
                    DoorId = door.Id,
                    PersonnelExists = true,
                    PersonnelIsActive = true,
                    DoorExists = true,
                    DoorIsActive = true,
                    ZoneIsActive = true,
                    FloorIsActive = true,
                    BuildingIsActive = true,
                    ActiveCards = [new AccessCardSnapshot { Id = card.Id, CardNumber = card.CardNumber, ValidFrom = card.ValidFrom, ValidUntil = card.ValidUntil }],
                    PersonnelDoorOverrides = overrides.Where(item => item.PersonnelId == personnelId && item.DoorId == door.Id)
                        .Select(item => ToRule(item.Id, item.IsAllowed, item.TimeScheduleId, scheduleActive, windows)).ToList(),
                    PersonnelZoneOverrides = overrides.Where(item => item.PersonnelId == personnelId && item.ZoneId == door.ZoneId)
                        .Select(item => ToRule(item.Id, item.IsAllowed, item.TimeScheduleId, scheduleActive, windows)).ToList(),
                    GroupDoorRules = doorRules.Where(item => groupIdsForPerson.Contains(item.AccessGroupId) && item.TargetId == door.Id)
                        .Select(item => ToRule(item.Id, item.IsAllowed, item.TimeScheduleId, scheduleActive, windows, item.Code)).ToList(),
                    GroupZoneRules = zoneRules.Where(item => groupIdsForPerson.Contains(item.AccessGroupId) && item.TargetId == door.ZoneId)
                        .Select(item => ToRule(item.Id, item.IsAllowed, item.TimeScheduleId, scheduleActive, windows, item.Code)).ToList()
                };
                var assigned = AccessRuleResolver.Assign(snapshot);
                if (assigned is not { IsAllowed: true })
                    continue;

                channels.Add(new ChannelRow
                {
                    ControllerId = door.ControllerId,
                    ControllerCode = door.ControllerCode,
                    DoorId = door.Id,
                    DoorName = door.Name,
                    ChannelNumber = door.ChannelNumber,
                    TimeScheduleId = assigned.TimeScheduleId,
                    TimeScheduleCode = scheduleCodes.TryGetValue(assigned.TimeScheduleId, out var code) ? code : "",
                    Windows = assigned.Windows
                });
            }

            results.Add(new PersonnelDeviceProjection
            {
                PersonnelId = personnelId,
                PersonnelNumber = person.PersonnelNumber,
                CardId = card.Id,
                CardNumber = card.CardNumber,
                ValidFrom = card.ValidFrom,
                ValidUntil = card.ValidUntil,
                Controllers = channels
                    .GroupBy(item => item.ControllerId)
                    .Select(group => new ControllerAccessProjection
                    {
                        ControllerId = group.Key,
                        ControllerCode = group.First().ControllerCode,
                        Channels = group
                            .OrderBy(item => item.ChannelNumber)
                            .Select(item => new DeviceChannelProjection
                            {
                                DoorId = item.DoorId,
                                DoorName = item.DoorName,
                                ChannelNumber = item.ChannelNumber,
                                TimeScheduleId = item.TimeScheduleId,
                                TimeScheduleCode = item.TimeScheduleCode,
                                Windows = item.Windows
                            })
                            .ToList()
                    })
                    .OrderBy(item => item.ControllerCode, StringComparer.Ordinal)
                    .ToList()
            });
        }

        return results;
    }

    private static async Task<List<int>> CandidatePersonnelAsync(
        UnAccessDbContext db,
        IReadOnlyList<int> doorIds,
        IReadOnlyList<int> zoneIds,
        DateOnly localDate,
        CancellationToken cancellationToken)
    {
        var fromGroups = await (
            from membership in db.PersonnelAccessGroups.AsNoTracking()
            join person in db.Personnel.AsNoTracking() on membership.PersonnelId equals person.Id
            where membership.IsActive
                && person.IsActive
                && membership.ValidFrom <= localDate
                && (membership.ValidUntil == null || membership.ValidUntil >= localDate)
                && db.Cards.Any(card => card.PersonnelId == person.Id && card.Status == CardStatus.Active)
                && (db.AccessGroupZones.Any(rule => rule.IsActive && rule.AccessGroupId == membership.AccessGroupId && zoneIds.Contains(rule.ZoneId))
                    || db.AccessGroupDoors.Any(rule => rule.IsActive && rule.AccessGroupId == membership.AccessGroupId && doorIds.Contains(rule.DoorId)))
            select person.Id).Distinct().ToListAsync(cancellationToken);

        var fromOverrides = await (
            from rule in db.PersonnelAccessOverrides.AsNoTracking()
            join person in db.Personnel.AsNoTracking() on rule.PersonnelId equals person.Id
            where rule.IsActive
                && person.IsActive
                && rule.ValidFrom <= localDate
                && (rule.ValidUntil == null || rule.ValidUntil >= localDate)
                && ((rule.DoorId != null && doorIds.Contains(rule.DoorId.Value)) || (rule.ZoneId != null && zoneIds.Contains(rule.ZoneId.Value)))
                && db.Cards.Any(card => card.PersonnelId == person.Id && card.Status == CardStatus.Active)
            select person.Id).Distinct().ToListAsync(cancellationToken);

        return fromGroups.Concat(fromOverrides).Distinct().ToList();
    }

    private static async Task<List<DoorRow>> LoadDoorsAsync(UnAccessDbContext db, int? controllerId, CancellationToken cancellationToken)
    {
        var query =
            from door in db.Doors.AsNoTracking()
            join zone in db.Zones.AsNoTracking() on door.ZoneId equals zone.Id
            join floor in db.Floors.AsNoTracking() on zone.FloorId equals floor.Id
            join building in db.Buildings.AsNoTracking() on floor.BuildingId equals building.Id
            join controller in db.Controllers.AsNoTracking() on door.ControllerId equals controller.Id
            where door.IsActive && zone.IsActive && floor.IsActive && building.IsActive && controller.IsActive && door.ControllerId != null
            select new DoorRow
            {
                Id = door.Id,
                Name = door.Name,
                ZoneId = zone.Id,
                ChannelNumber = door.ChannelNumber,
                ControllerId = controller.Id,
                ControllerCode = controller.Code
            };
        if (controllerId is int selected)
            query = query.Where(door => door.ControllerId == selected);
        return await query.ToListAsync(cancellationToken);
    }

    private static AccessRuleSnapshot ToRule(
        int id,
        bool isAllowed,
        int scheduleId,
        IReadOnlyDictionary<int, bool> scheduleActive,
        IReadOnlyDictionary<int, IReadOnlyList<TimeWindow>> windows,
        string sourceCode = "")
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

    private static DeviceChannelCommand ToCommand(DeviceChannelProjection channel) =>
        new()
        {
            ChannelNumber = channel.ChannelNumber,
            TimeScheduleId = channel.TimeScheduleId,
            TimeScheduleCode = channel.TimeScheduleCode,
            Windows = channel.Windows
        };

    private sealed class DoorRow
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public int ZoneId { get; init; }
        public int ChannelNumber { get; init; }
        public int ControllerId { get; init; }
        public string ControllerCode { get; init; } = "";
    }

    private sealed class PersonRow
    {
        public int Id { get; init; }
        public bool IsActive { get; init; }
        public string PersonnelNumber { get; init; } = "";
    }

    private sealed class CardRow
    {
        public int Id { get; init; }
        public int PersonnelId { get; init; }
        public string CardNumber { get; init; } = "";
        public DateOnly ValidFrom { get; init; }
        public DateOnly? ValidUntil { get; init; }
    }

    private sealed class MembershipRow
    {
        public int PersonnelId { get; init; }
        public int AccessGroupId { get; init; }
    }

    private sealed class RuleRow
    {
        public int Id { get; init; }
        public int AccessGroupId { get; init; }
        public int TargetId { get; init; }
        public bool IsAllowed { get; init; }
        public int TimeScheduleId { get; init; }
        public string Code { get; init; } = "";
    }

    private sealed class OverrideRow
    {
        public int Id { get; init; }
        public int PersonnelId { get; init; }
        public int? DoorId { get; init; }
        public int? ZoneId { get; init; }
        public bool IsAllowed { get; init; }
        public int TimeScheduleId { get; init; }
    }

    private sealed class ScheduleRow
    {
        public int Id { get; init; }
        public string Code { get; init; } = "";
        public bool IsActive { get; init; }
    }

    private sealed class DetailRow
    {
        public int TimeScheduleId { get; init; }
        public DayOfWeek DayOfWeek { get; init; }
        public TimeOnly StartTime { get; init; }
        public TimeOnly EndTime { get; init; }
    }

    private sealed class ChannelRow
    {
        public int ControllerId { get; init; }
        public string ControllerCode { get; init; } = "";
        public int DoorId { get; init; }
        public string DoorName { get; init; } = "";
        public int ChannelNumber { get; init; }
        public int TimeScheduleId { get; init; }
        public string TimeScheduleCode { get; init; } = "";
        public IReadOnlyList<TimeWindow> Windows { get; init; } = [];
    }
}
