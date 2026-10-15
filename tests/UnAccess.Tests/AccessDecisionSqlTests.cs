using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Access;
using UnAccess.Core.Entities;
using UnAccess.Core.Enums;
using UnAccess.Core.Models;
using UnAccess.Data;

namespace UnAccess.Tests;

[Collection("sql")]
public class AccessDecisionSqlTests
{
    private readonly SqlFixture _database;

    public AccessDecisionSqlTests(SqlFixture database)
    {
        _database = database;
    }

    [SkippableFact]
    public async Task Person_without_a_permission_is_denied()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewWorldAsync();
        var decision = await EvaluateAsync(world, world.At(10, 0));
        Assert.False(decision.IsAllowed);
        Assert.Equal(AccessRuleResolver.NoPermissionReason, decision.Reason);
        Assert.Equal(MatchedRuleType.None, decision.MatchedRuleType);
    }

    [SkippableFact]
    public async Task Group_zone_permission_allows_the_door()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewWorldAsync();
        await GrantZoneAsync(world, world.AllowGroup, allowed: true, world.FullTime);
        var decision = await EvaluateAsync(world, world.At(10, 0));
        Assert.True(decision.IsAllowed);
        Assert.Equal(MatchedRuleType.AccessGroupZone, decision.MatchedRuleType);
        Assert.Contains(world.AllowCode, decision.Reason, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task Door_deny_overrides_zone_allow()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewWorldAsync();
        await GrantZoneAsync(world, world.AllowGroup, allowed: true, world.FullTime);
        await GrantDoorAsync(world, world.AllowGroup, allowed: false, world.FullTime);
        var decision = await EvaluateAsync(world, world.At(10, 0));
        Assert.False(decision.IsAllowed);
        Assert.Equal(MatchedRuleType.AccessGroupDoor, decision.MatchedRuleType);
    }

    [SkippableFact]
    public async Task Personal_allow_overrides_group_deny()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewWorldAsync();
        await GrantZoneAsync(world, world.AllowGroup, allowed: false, world.FullTime);
        await OverrideAsync(world, allowed: true, world.FullTime, validUntil: null);
        var decision = await EvaluateAsync(world, world.At(10, 0));
        Assert.True(decision.IsAllowed);
        Assert.Equal(MatchedRuleType.PersonnelDoorOverride, decision.MatchedRuleType);
        Assert.Equal("Kişisel izin nedeniyle geçiş onaylandı.", decision.Reason);
        var view = await Service<IPersonnelAuthorizationService>().GetAsync(world.PersonnelId, CancellationToken.None);
        Assert.Contains(view.Groups, item => item.Code == world.AllowCode);
        Assert.Contains(view.Overrides, item => item.DoorId == world.DoorId && item.IsAllowed);
        Assert.Contains(view.Effective, item => item.DoorId == world.DoorId && item.IsAllowed && item.SourceName == "KİŞİSEL İSTİSNA");
    }

    [SkippableFact]
    public async Task Personal_deny_overrides_group_allow()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewWorldAsync();
        await GrantZoneAsync(world, world.AllowGroup, allowed: true, world.FullTime);
        await OverrideAsync(world, allowed: false, world.FullTime, validUntil: null);
        var decision = await EvaluateAsync(world, world.At(10, 0));
        Assert.False(decision.IsAllowed);
        Assert.Equal(MatchedRuleType.PersonnelDoorOverride, decision.MatchedRuleType);
        Assert.Equal("Kişisel yasak nedeniyle geçiş reddedildi.", decision.Reason);
    }

    [SkippableFact]
    public async Task Deny_wins_between_two_groups()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewWorldAsync();
        await GrantZoneAsync(world, world.AllowGroup, allowed: true, world.FullTime);
        await GrantZoneAsync(world, world.DenyGroup, allowed: false, world.FullTime);
        var decision = await EvaluateAsync(world, world.At(10, 0));
        Assert.False(decision.IsAllowed);
        Assert.Equal(MatchedRuleType.AccessGroupZone, decision.MatchedRuleType);
        Assert.Contains(world.DenyCode, decision.Reason, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task Office_hours_allow_inside_and_deny_outside()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewWorldAsync();
        await GrantZoneAsync(world, world.AllowGroup, allowed: true, world.OfficeTime);
        var inside = await EvaluateAsync(world, world.At(10, 0));
        var outside = await EvaluateAsync(world, world.At(21, 0));
        Assert.True(inside.IsAllowed);
        Assert.False(outside.IsAllowed);
        Assert.Equal(AccessRuleResolver.OutsideScheduleReason, outside.Reason);
    }

    [SkippableFact]
    public async Task Night_schedule_allows_both_sides_of_midnight()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewWorldAsync();
        await GrantZoneAsync(world, world.AllowGroup, allowed: true, world.NightTime);
        Assert.True((await EvaluateAsync(world, world.At(23, 0))).IsAllowed);
        Assert.True((await EvaluateAsync(world, new DateTime(2026, 10, 16, 2, 0, 0))).IsAllowed);
        var afternoon = await EvaluateAsync(world, world.At(14, 0));
        Assert.False(afternoon.IsAllowed);
        Assert.Equal(AccessRuleResolver.OutsideScheduleReason, afternoon.Reason);
    }

    [SkippableFact]
    public async Task Inactive_person_inactive_door_cancelled_and_expired_cards_are_denied()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewWorldAsync();
        await GrantZoneAsync(world, world.AllowGroup, allowed: true, world.FullTime);

        await Service<ICardService>().SetStatusAsync(world.CardId, CardStatus.Cancelled, CancellationToken.None);
        var cancelled = await EvaluateAsync(world, world.At(10, 0));
        Assert.False(cancelled.IsAllowed);
        Assert.Equal("Aktif kartı yok.", cancelled.Reason);

        var expiredCard = await Service<ICardService>().CreateAsync(new CardEditModel
        {
            PersonnelId = world.PersonnelId,
            CardNumber = "E" + Guid.NewGuid().ToString("N")[..10],
            ValidFrom = new DateOnly(2020, 1, 1),
            ValidUntil = new DateOnly(2020, 1, 2)
        }, CancellationToken.None);
        var expired = await EvaluateAsync(world, world.At(10, 0));
        Assert.False(expired.IsAllowed);
        Assert.Equal("Kartın geçerlilik tarihi uygun değil.", expired.Reason);
        Assert.Equal(expiredCard.CardNumber, expired.CardNumber);

        await Service<IPersonnelService>().UpdateAsync(world.PersonnelId, world.Edit(false), CancellationToken.None);
        var passivePerson = await EvaluateAsync(world, world.At(10, 0));
        Assert.False(passivePerson.IsAllowed);
        Assert.Equal("Personel pasif.", passivePerson.Reason);

        var doorWorld = await NewWorldAsync();
        await GrantZoneAsync(doorWorld, doorWorld.AllowGroup, allowed: true, doorWorld.FullTime);
        var door = await Service<IHospitalStructureService>().GetDoorAsync(doorWorld.DoorId, CancellationToken.None);
        Assert.NotNull(door);
        door.IsActive = false;
        await Service<IHospitalStructureService>().UpdateDoorAsync(doorWorld.DoorId, door, CancellationToken.None);
        var passiveDoor = await EvaluateAsync(doorWorld, doorWorld.At(10, 0));
        Assert.False(passiveDoor.IsAllowed);
        Assert.Equal("Kapı pasif.", passiveDoor.Reason);
    }

    [SkippableFact]
    public async Task Expired_personal_override_is_not_applied()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewWorldAsync();
        await GrantZoneAsync(world, world.AllowGroup, allowed: false, world.FullTime);
        await OverrideAsync(world, allowed: true, world.FullTime, validUntil: new DateOnly(2026, 10, 14));
        var decision = await EvaluateAsync(world, world.At(10, 0));
        Assert.False(decision.IsAllowed);
        Assert.Equal(MatchedRuleType.AccessGroupZone, decision.MatchedRuleType);
        Assert.Contains(world.AllowCode, decision.Reason, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task Personal_server_room_schedule_does_not_fall_back_to_cerrah_group()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        await using var db = await Factory().CreateDbContextAsync();
        var surgeon = await db.AccessGroups.AsNoTracking().SingleAsync(item => item.Code == "CERRAH");
        var door = await db.Doors.AsNoTracking().SingleAsync(item => item.Code == "SERVER");
        var full = await db.TimeSchedules.AsNoTracking().SingleAsync(item => item.Code == "7_24");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var office = await Service<ITimeScheduleService>().SaveAsync(
            Schedule("MS" + suffix, "Mesai " + suffix, OfficeDays()),
            CancellationToken.None);
        var personnel = Service<IPersonnelService>();
        var departments = await personnel.GetDepartmentsAsync(CancellationToken.None);
        var jobs = await personnel.GetJobTitlesAsync(CancellationToken.None);
        var person = await personnel.CreateAsync(new PersonnelEditModel
        {
            PersonnelNumber = "AHT" + suffix,
            FirstName = "Ahmet",
            LastName = "Yılmaz",
            DepartmentId = departments.Single(item => item.Code == "GC").Id,
            JobTitleId = jobs.Single(item => item.Code == "DOKTOR").Id,
            IsActive = true
        }, CancellationToken.None);
        await Service<ICardService>().CreateAsync(new CardEditModel
        {
            PersonnelId = person.Id,
            CardNumber = "AH" + suffix,
            ValidFrom = new DateOnly(2026, 1, 1)
        }, CancellationToken.None);
        await Service<IPersonnelAuthorizationService>().AssignGroupAsync(new PersonnelGroupEditModel
        {
            PersonnelId = person.Id,
            AccessGroupId = surgeon.Id,
            ValidFrom = new DateOnly(2026, 1, 1),
            IsActive = true
        }, CancellationToken.None);
        await Service<IAccessGroupService>().SetDoorRuleAsync(surgeon.Id, door.Id, full.Id, true, CancellationToken.None);
        await Service<IPersonnelAuthorizationService>().SaveOverrideAsync(new PersonnelOverrideEditModel
        {
            PersonnelId = person.Id,
            DoorId = door.Id,
            TimeScheduleId = office.Id,
            IsAllowed = true,
            ValidFrom = new DateOnly(2026, 1, 1),
            IsActive = true,
            Reason = "Mesai içi kişisel izin"
        }, CancellationToken.None);

        var service = Service<IAccessDecisionService>();
        var morning = await service.EvaluateAsync(person.Id, door.Id, new DateTime(2026, 10, 15, 10, 0, 0), CancellationToken.None);
        var night = await service.EvaluateAsync(person.Id, door.Id, new DateTime(2026, 10, 15, 22, 0, 0), CancellationToken.None);

        Assert.True(morning.IsAllowed);
        Assert.Equal(MatchedRuleType.PersonnelDoorOverride, morning.MatchedRuleType);
        Assert.Equal(office.Id, morning.TimeScheduleId);
        Assert.False(night.IsAllowed);
        Assert.Equal(AccessRuleResolver.OutsideScheduleReason, night.Reason);
        Assert.Equal(MatchedRuleType.PersonnelDoorOverride, night.MatchedRuleType);
        Assert.Equal(office.Id, night.TimeScheduleId);
        Assert.NotEqual(full.Id, night.TimeScheduleId);
    }

    [SkippableFact]
    public async Task Personal_zone_outside_schedule_does_not_use_group_door_allow()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewWorldAsync();
        await GrantDoorAsync(world, world.AllowGroup, allowed: true, world.FullTime);
        await OverrideZoneAsync(world, allowed: true, world.OfficeTime);
        var morning = await EvaluateAsync(world, world.At(10, 0));
        var night = await EvaluateAsync(world, world.At(22, 0));

        Assert.True(morning.IsAllowed);
        Assert.Equal(MatchedRuleType.PersonnelZoneOverride, morning.MatchedRuleType);
        Assert.False(night.IsAllowed);
        Assert.Equal(AccessRuleResolver.OutsideScheduleReason, night.Reason);
        Assert.Equal(MatchedRuleType.PersonnelZoneOverride, night.MatchedRuleType);
        Assert.Equal(world.OfficeTime, night.TimeScheduleId);
    }

    [SkippableFact]
    public async Task Group_door_outside_schedule_does_not_use_group_zone_allow()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewWorldAsync();
        await GrantZoneAsync(world, world.AllowGroup, allowed: true, world.FullTime);
        await GrantDoorAsync(world, world.AllowGroup, allowed: true, world.OfficeTime);
        var morning = await EvaluateAsync(world, world.At(10, 0));
        var night = await EvaluateAsync(world, world.At(22, 0));

        Assert.True(morning.IsAllowed);
        Assert.Equal(MatchedRuleType.AccessGroupDoor, morning.MatchedRuleType);
        Assert.False(night.IsAllowed);
        Assert.Equal(AccessRuleResolver.OutsideScheduleReason, night.Reason);
        Assert.Equal(MatchedRuleType.AccessGroupDoor, night.MatchedRuleType);
        Assert.Equal(world.OfficeTime, night.TimeScheduleId);
    }

    [SkippableFact]
    public async Task Simulator_records_granted_and_denied_events_and_rejects_a_duplicate_event_id()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewWorldAsync();
        await GrantZoneAsync(world, world.AllowGroup, allowed: true, world.FullTime);
        var granted = await Service<IPassageRecorder>().RecordAsync(world.PersonnelId, world.DoorId, world.At(10, 0), CancellationToken.None);
        var denied = await Service<IPassageRecorder>().RecordAsync(world.PersonnelId, world.OtherDoorId, world.At(10, 0), CancellationToken.None);

        await using var db = await Factory().CreateDbContextAsync();
        var grantedRow = await db.AccessEvents.SingleAsync(item => item.EventId == granted.EventId);
        var deniedRow = await db.AccessEvents.SingleAsync(item => item.EventId == denied.EventId);
        Assert.Equal(nameof(AccessDecisionKind.Granted), grantedRow.Decision);
        Assert.Equal(nameof(AccessDecisionKind.Denied), deniedRow.Decision);
        Assert.Equal(AccessEventSources.Simulator, grantedRow.Source);
        Assert.Equal(AccessEventSources.Simulator, deniedRow.Source);
        Assert.Equal(world.DoorId, grantedRow.DoorId);
        Assert.False(string.IsNullOrWhiteSpace(grantedRow.Reason));
        Assert.False(string.IsNullOrWhiteSpace(deniedRow.Reason));

        db.AccessEvents.Add(new AccessEvent
        {
            EventId = grantedRow.EventId,
            DoorId = world.DoorId,
            CardNumber = "1",
            EventType = AccessEventTypes.CardRead,
            Decision = nameof(AccessDecisionKind.Unknown),
            Reason = "Tekrar",
            OccurredAtUtc = DateTime.UtcNow,
            ReceivedAtUtc = DateTime.UtcNow,
            Source = AccessEventSources.Simulator
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private async Task<AccessDecision> EvaluateAsync(World world, DateTime at) =>
        await Service<IAccessDecisionService>().EvaluateAsync(world.PersonnelId, world.DoorId, at, CancellationToken.None);

    private async Task GrantZoneAsync(World world, int groupId, bool allowed, int scheduleId) =>
        await Service<IAccessGroupService>().SetZoneRuleAsync(groupId, world.ZoneId, scheduleId, allowed, CancellationToken.None);

    private async Task GrantDoorAsync(World world, int groupId, bool allowed, int scheduleId) =>
        await Service<IAccessGroupService>().SetDoorRuleAsync(groupId, world.DoorId, scheduleId, allowed, CancellationToken.None);

    private async Task OverrideAsync(World world, bool allowed, int scheduleId, DateOnly? validUntil) =>
        await Service<IPersonnelAuthorizationService>().SaveOverrideAsync(new PersonnelOverrideEditModel
        {
            PersonnelId = world.PersonnelId,
            DoorId = world.DoorId,
            TimeScheduleId = scheduleId,
            IsAllowed = allowed,
            ValidFrom = new DateOnly(2026, 1, 1),
            ValidUntil = validUntil,
            IsActive = true
        }, CancellationToken.None);

    private async Task OverrideZoneAsync(World world, bool allowed, int scheduleId) =>
        await Service<IPersonnelAuthorizationService>().SaveOverrideAsync(new PersonnelOverrideEditModel
        {
            PersonnelId = world.PersonnelId,
            ZoneId = world.ZoneId,
            TimeScheduleId = scheduleId,
            IsAllowed = allowed,
            ValidFrom = new DateOnly(2026, 1, 1),
            IsActive = true
        }, CancellationToken.None);

    private async Task<World> NewWorldAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var hospital = Service<IHospitalStructureService>();
        var building = await hospital.CreateBuildingAsync(new BuildingEditModel { Code = "B" + suffix, Name = "Bina " + suffix, IsActive = true }, CancellationToken.None);
        var floor = await hospital.CreateFloorAsync(new FloorEditModel
        {
            BuildingId = building.Id,
            Code = "F",
            Name = "Kat",
            FloorNumber = 1,
            IsActive = true
        }, CancellationToken.None);
        var zone = await hospital.CreateZoneAsync(new ZoneEditModel
        {
            FloorId = floor.Id,
            Code = "Z",
            Name = "Bolge " + suffix,
            IsActive = true
        }, CancellationToken.None);
        var door = await hospital.CreateDoorAsync(Door(zone.Id, "D1" + suffix), CancellationToken.None);
        var otherZone = await hospital.CreateZoneAsync(new ZoneEditModel
        {
            FloorId = floor.Id,
            Code = "Z2",
            Name = "Diger " + suffix,
            IsActive = true
        }, CancellationToken.None);
        var other = await hospital.CreateDoorAsync(Door(otherZone.Id, "D2" + suffix), CancellationToken.None);
        var schedules = Service<ITimeScheduleService>();
        var full = await schedules.SaveAsync(Schedule("F" + suffix, "Tam " + suffix, AllDays(new TimeOnly(0, 0), new TimeOnly(0, 0))), CancellationToken.None);
        var office = await schedules.SaveAsync(Schedule("M" + suffix, "Mesai " + suffix, OfficeDays()), CancellationToken.None);
        var night = await schedules.SaveAsync(Schedule("N" + suffix, "Gece " + suffix, AllDays(new TimeOnly(22, 0), new TimeOnly(6, 0))), CancellationToken.None);
        var groups = Service<IAccessGroupService>();
        var allowCode = "A" + suffix;
        var denyCode = "Y" + suffix;
        var allow = await groups.SaveAsync(null, new AccessGroupEditModel { Code = allowCode, Name = allowCode, IsActive = true }, CancellationToken.None);
        var deny = await groups.SaveAsync(null, new AccessGroupEditModel { Code = denyCode, Name = denyCode, IsActive = true }, CancellationToken.None);
        var personnel = Service<IPersonnelService>();
        var departments = await personnel.GetDepartmentsAsync(CancellationToken.None);
        var jobs = await personnel.GetJobTitlesAsync(CancellationToken.None);
        var number = "P" + suffix;
        var person = await personnel.CreateAsync(new PersonnelEditModel
        {
            PersonnelNumber = number,
            FirstName = "Yetki",
            LastName = "Test",
            DepartmentId = departments.Single(item => item.Code == "GC").Id,
            JobTitleId = jobs.Single(item => item.Code == "DOKTOR").Id,
            IsActive = true
        }, CancellationToken.None);
        var card = await Service<ICardService>().CreateAsync(new CardEditModel
        {
            PersonnelId = person.Id,
            CardNumber = "C" + suffix + "01",
            ValidFrom = new DateOnly(2026, 1, 1)
        }, CancellationToken.None);
        var auth = Service<IPersonnelAuthorizationService>();
        await auth.AssignGroupAsync(new PersonnelGroupEditModel
        {
            PersonnelId = person.Id,
            AccessGroupId = allow.Id,
            ValidFrom = new DateOnly(2026, 1, 1),
            IsActive = true
        }, CancellationToken.None);
        await auth.AssignGroupAsync(new PersonnelGroupEditModel
        {
            PersonnelId = person.Id,
            AccessGroupId = deny.Id,
            ValidFrom = new DateOnly(2026, 1, 1),
            IsActive = true
        }, CancellationToken.None);

        return new World(person.Id, card.Id, zone.Id, door.Id, other.Id, allow.Id, deny.Id, allowCode, denyCode, full.Id, office.Id, night.Id, person);
    }

    private static DoorEditModel Door(int zoneId, string code) => new()
    {
        ZoneId = zoneId,
        Code = code,
        Name = code,
        ChannelNumber = 1,
        IsActive = true
    };

    private static TimeScheduleEditModel Schedule(string code, string name, IReadOnlyList<TimeIntervalModel> intervals) => new()
    {
        Code = code,
        Name = name,
        IsActive = true,
        Intervals = intervals
    };

    private static List<TimeIntervalModel> AllDays(TimeOnly start, TimeOnly end) =>
        Enum.GetValues<DayOfWeek>().Select(day => new TimeIntervalModel { DayOfWeek = day, StartTime = start, EndTime = end }).ToList();

    private static List<TimeIntervalModel> OfficeDays() =>
        new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
            .Select(day => new TimeIntervalModel { DayOfWeek = day, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(18, 0) })
            .ToList();

    private T Service<T>() where T : notnull => _database.Provider!.GetRequiredService<T>();

    private IDbContextFactory<UnAccessDbContext> Factory() =>
        _database.Provider!.GetRequiredService<IDbContextFactory<UnAccessDbContext>>();

    private sealed record World(
        int PersonnelId,
        int CardId,
        int ZoneId,
        int DoorId,
        int OtherDoorId,
        int AllowGroup,
        int DenyGroup,
        string AllowCode,
        string DenyCode,
        int FullTime,
        int OfficeTime,
        int NightTime,
        PersonnelDetail Person)
    {
        public DateTime At(int hour, int minute) => new(2026, 10, 15, hour, minute, 0, DateTimeKind.Unspecified);

        public PersonnelEditModel Edit(bool isActive) => new()
        {
            PersonnelNumber = Person.PersonnelNumber,
            FirstName = Person.FirstName,
            LastName = Person.LastName,
            DepartmentId = Person.DepartmentId,
            JobTitleId = Person.JobTitleId,
            IsActive = isActive
        };
    }
}
