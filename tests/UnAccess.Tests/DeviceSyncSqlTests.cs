using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Audit;
using UnAccess.Core.Entities;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Enums;
using UnAccess.Core.Models;
using UnAccess.Core.Sync;
using UnAccess.Data;
using UnAccess.Data.Infrastructure;
using UnAccess.Data.Services;
using UnAccess.DeviceService;
using UnAccess.Devices;

namespace UnAccess.Tests;

[Collection("sql")]
public class DeviceSyncSqlTests
{
    private readonly SqlFixture _database;

    public DeviceSyncSqlTests(SqlFixture database)
    {
        _database = database;
    }

    [SkippableFact]
    public async Task Active_card_enqueues_upsert()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewPersonAsync("U");
        var jobs = await JobsAsync(DeviceSyncOperations.UpsertCard, world.CardId.ToString());
        Assert.Contains(jobs, job => job.Status == DeviceSyncStatus.Pending && job.Payload.Contains(world.CardNumber, StringComparison.Ordinal));
    }

    [SkippableFact]
    public async Task Lost_card_enqueues_remove()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewPersonAsync("L");
        await Service<ICardService>().SetStatusAsync(world.CardId, CardStatus.Lost, CancellationToken.None);
        Assert.Contains(await JobsAsync(DeviceSyncOperations.RemoveCard, world.CardId.ToString()), job => job.Status == DeviceSyncStatus.Pending);
    }

    [SkippableFact]
    public async Task Cancelled_card_enqueues_remove()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewPersonAsync("X");
        await Service<ICardService>().SetStatusAsync(world.CardId, CardStatus.Cancelled, CancellationToken.None);
        Assert.Contains(await JobsAsync(DeviceSyncOperations.RemoveCard, world.CardId.ToString()), job => job.Status == DeviceSyncStatus.Pending);
    }

    [SkippableFact]
    public async Task Deactivated_personnel_enqueues_remove_for_the_active_card()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewPersonAsync("D");
        await Service<IPersonnelService>().UpdateAsync(world.PersonnelId, world.Edit(false), CancellationToken.None);
        Assert.Contains(await JobsAsync(DeviceSyncOperations.RemoveCard, world.CardId.ToString()), job => job.Status == DeviceSyncStatus.Pending);
    }

    [SkippableFact]
    public async Task Assigning_and_removing_a_group_enqueues_permission_sync()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewPersonAsync("G");
        var group = await NewGroupAsync("GR");
        await Service<IPersonnelAuthorizationService>().AssignGroupAsync(Membership(world, group), CancellationToken.None);
        Assert.Contains(await JobsAsync(DeviceSyncOperations.SyncCardPermissions, world.PersonnelId.ToString()), job => job.Status == DeviceSyncStatus.Pending);

        await CompleteJobsAsync(DeviceSyncOperations.SyncCardPermissions, world.PersonnelId.ToString());
        await Service<IPersonnelAuthorizationService>().RemoveGroupAsync(world.PersonnelId, group, CancellationToken.None);
        Assert.Contains(await JobsAsync(DeviceSyncOperations.SyncCardPermissions, world.PersonnelId.ToString()), job => job.Status == DeviceSyncStatus.Pending);
    }

    [SkippableFact]
    public async Task Changing_an_override_enqueues_permission_sync()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewPersonAsync("O");
        var schedule = await Service<ITimeScheduleService>().SaveAsync(FullSchedule("OS"), CancellationToken.None);
        await Service<IPersonnelAuthorizationService>().SaveOverrideAsync(new PersonnelOverrideEditModel
        {
            PersonnelId = world.PersonnelId,
            DoorId = world.DoorId,
            TimeScheduleId = schedule.Id,
            IsAllowed = true,
            ValidFrom = new DateOnly(2026, 1, 1),
            IsActive = true
        }, CancellationToken.None);
        Assert.Contains(await JobsAsync(DeviceSyncOperations.SyncCardPermissions, world.PersonnelId.ToString()), job => job.Status == DeviceSyncStatus.Pending);
    }

    [SkippableFact]
    public async Task Changing_a_group_door_rule_enqueues_members()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewPersonAsync("GD");
        var group = await NewGroupAsync("GD");
        await Service<IPersonnelAuthorizationService>().AssignGroupAsync(Membership(world, group), CancellationToken.None);
        await CompleteJobsAsync(DeviceSyncOperations.SyncCardPermissions, world.PersonnelId.ToString());
        var schedule = await Service<ITimeScheduleService>().SaveAsync(FullSchedule("DS"), CancellationToken.None);
        await Service<IAccessGroupService>().SetDoorRuleAsync(group, world.DoorId, schedule.Id, true, CancellationToken.None);
        Assert.Contains(await JobsAsync(DeviceSyncOperations.SyncCardPermissions, world.PersonnelId.ToString()), job => job.Status == DeviceSyncStatus.Pending);
    }

    [SkippableFact]
    public async Task Changing_a_schedule_enqueues_affected_personnel()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewPersonAsync("TS");
        var group = await NewGroupAsync("TS");
        var schedule = await Service<ITimeScheduleService>().SaveAsync(FullSchedule("TS"), CancellationToken.None);
        await Service<IPersonnelAuthorizationService>().AssignGroupAsync(Membership(world, group), CancellationToken.None);
        await Service<IAccessGroupService>().SetZoneRuleAsync(group, world.ZoneId, schedule.Id, true, CancellationToken.None);
        await CompleteJobsAsync(DeviceSyncOperations.SyncCardPermissions, world.PersonnelId.ToString());

        var edit = await Service<ITimeScheduleService>().GetAsync(schedule.Id, CancellationToken.None);
        Assert.NotNull(edit);
        edit.Intervals = [new TimeIntervalModel { DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0) }];
        await Service<ITimeScheduleService>().SaveAsync(edit, CancellationToken.None);
        Assert.Contains(await JobsAsync(DeviceSyncOperations.SyncCardPermissions, world.PersonnelId.ToString()), job => job.Status == DeviceSyncStatus.Pending);
    }

    [SkippableFact]
    public async Task Card_and_queue_roll_back_together()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewPersonAsync("RB", withCard: false);
        await using var probe = await Factory().CreateDbContextAsync();
        var options = new DbContextOptionsBuilder<UnAccessDbContext>()
            .UseSqlServer(probe.Database.GetConnectionString())
            .AddInterceptors(new SecondSaveFails())
            .Options;
        var cards = new CardService(
            new FixedFactory(options),
            new AuditWriter(),
            NullLogger<CardService>.Instance,
            new DeviceSyncOutbox(new SystemUtcClock(), new DeviceSyncOptions()));
        var number = "RB" + Guid.NewGuid().ToString("N")[..10];
        await Assert.ThrowsAnyAsync<Exception>(() => cards.CreateAsync(new CardEditModel
        {
            PersonnelId = world.PersonnelId,
            CardNumber = number,
            ValidFrom = new DateOnly(2026, 1, 1)
        }, CancellationToken.None));

        await using var db = await Factory().CreateDbContextAsync();
        Assert.False(await db.Cards.AnyAsync(card => card.CardNumber == number));
        Assert.False(await db.DeviceSyncQueue.AnyAsync(job => job.Payload.Contains(number)));
    }

    [SkippableFact]
    public async Task Worker_completes_a_pending_upsert()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewPersonAsync("WK");
        var job = await SinglePendingAsync(DeviceSyncOperations.UpsertCard, world.CardId.ToString());
        await IsolateAsync(job.Id);
        var registry = new SimulatorControllerRegistry();
        var clock = new FakeUtcClock { UtcNow = DateTime.UtcNow.AddHours(1) };
        await Worker(clock, registry).ProcessBatchAsync(CancellationToken.None);

        var stored = await ReloadAsync(job.Id);
        Assert.Equal(DeviceSyncStatus.Completed, stored.Status);
        Assert.Contains(registry.Get(job.ControllerId).GetSnapshot().Cards, card => card.CardNumber == world.CardNumber);
    }

    [SkippableFact]
    public async Task Offline_simulator_retries_then_completes_when_online()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewPersonAsync("OF");
        var job = await SinglePendingAsync(DeviceSyncOperations.UpsertCard, world.CardId.ToString());
        await IsolateAsync(job.Id);
        var registry = new SimulatorControllerRegistry();
        var device = registry.Get(job.ControllerId);
        device.SetOffline(true);
        var clock = new FakeUtcClock { UtcNow = DateTime.UtcNow.AddHours(1) };
        var worker = Worker(clock, registry);
        await worker.ProcessBatchAsync(CancellationToken.None);

        var failed = await ReloadAsync(job.Id);
        Assert.Equal(DeviceSyncStatus.Failed, failed.Status);
        Assert.Equal(1, failed.AttemptCount);
        Assert.True(failed.NextAttemptAtUtc > clock.UtcNow);

        await worker.ProcessBatchAsync(CancellationToken.None);
        Assert.Equal(DeviceSyncStatus.Failed, (await ReloadAsync(job.Id)).Status);

        device.SetOffline(false);
        clock.UtcNow = failed.NextAttemptAtUtc.AddSeconds(1);
        await worker.ProcessBatchAsync(CancellationToken.None);
        Assert.Equal(DeviceSyncStatus.Completed, (await ReloadAsync(job.Id)).Status);
        Assert.Contains(device.GetSnapshot().Cards, card => card.CardNumber == world.CardNumber);
    }

    [SkippableFact]
    public async Task Exhausted_attempts_become_dead_letter()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewPersonAsync("DL");
        var job = await SinglePendingAsync(DeviceSyncOperations.UpsertCard, world.CardId.ToString());
        await IsolateAsync(job.Id);
        await using (var db = await Factory().CreateDbContextAsync())
        {
            var row = await db.DeviceSyncQueue.SingleAsync(item => item.Id == job.Id);
            row.MaxAttempts = 1;
            await db.SaveChangesAsync();
        }

        var registry = new SimulatorControllerRegistry();
        registry.Get(job.ControllerId).SetOffline(true);
        var clock = new FakeUtcClock { UtcNow = DateTime.UtcNow.AddHours(1) };
        await Worker(clock, registry).ProcessBatchAsync(CancellationToken.None);
        var dead = await ReloadAsync(job.Id);
        Assert.Equal(DeviceSyncStatus.DeadLetter, dead.Status);

        clock.UtcNow = clock.UtcNow.AddHours(2);
        await Worker(clock, registry).ProcessBatchAsync(CancellationToken.None);
        Assert.Equal(DeviceSyncStatus.DeadLetter, (await ReloadAsync(job.Id)).Status);
    }

    [SkippableFact]
    public async Task Stale_processing_job_is_recovered()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewPersonAsync("ST");
        var job = await SinglePendingAsync(DeviceSyncOperations.UpsertCard, world.CardId.ToString());
        await IsolateAsync(job.Id);
        var clock = new FakeUtcClock { UtcNow = DateTime.UtcNow.AddHours(1) };
        await using (var db = await Factory().CreateDbContextAsync())
        {
            var row = await db.DeviceSyncQueue.SingleAsync(item => item.Id == job.Id);
            row.Status = DeviceSyncStatus.Processing;
            row.StartedAtUtc = clock.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync();
        }

        var registry = new SimulatorControllerRegistry();
        await Worker(clock, registry).ProcessBatchAsync(CancellationToken.None);
        Assert.Equal(DeviceSyncStatus.Completed, (await ReloadAsync(job.Id)).Status);
    }

    [SkippableFact]
    public async Task Same_active_channel_cannot_be_used_twice()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var controllerId = await SimulatorIdAsync();
        var zone = await NewZoneAsync("CH");
        var hospital = Service<IHospitalStructureService>();
        await hospital.CreateDoorAsync(Door(zone, "C1", controllerId, 90), CancellationToken.None);
        var error = await Assert.ThrowsAsync<UnAccessException>(() => hospital.CreateDoorAsync(Door(zone, "C2", controllerId, 90), CancellationToken.None));
        Assert.Equal("Bu kanal aynı kontrolörde başka bir aktif kapıya atanmış.", error.UserMessage);
    }

    [SkippableFact]
    public async Task Projection_returns_allowed_channels_and_keeps_the_schedule()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewPersonAsync("PR");
        var blocked = await Service<IHospitalStructureService>().CreateDoorAsync(Door(world.ZoneId, "B", world.ControllerId, NextChannel()), CancellationToken.None);
        var schedule = await Service<ITimeScheduleService>().SaveAsync(FullSchedule("PR"), CancellationToken.None);
        var group = await NewGroupAsync("PR");
        await Service<IPersonnelAuthorizationService>().AssignGroupAsync(Membership(world, group), CancellationToken.None);
        await Service<IAccessGroupService>().SetZoneRuleAsync(group, world.ZoneId, schedule.Id, true, CancellationToken.None);
        await Service<IAccessGroupService>().SetDoorRuleAsync(group, blocked.Id, schedule.Id, false, CancellationToken.None);

        var projection = await Service<IDevicePermissionProjectionService>().ProjectPersonnelAsync(world.PersonnelId, CancellationToken.None);
        var controller = Assert.Single(projection.Controllers, item => item.ControllerId == world.ControllerId);
        var channel = Assert.Single(controller.Channels);
        Assert.Equal(world.Channel, channel.ChannelNumber);
        Assert.Equal(schedule.Id, channel.TimeScheduleId);
        Assert.Equal(schedule.Code, channel.TimeScheduleCode);
        Assert.NotEmpty(channel.Windows);
    }

    [SkippableFact]
    public async Task Full_sync_matches_sql_removes_old_cards_and_is_idempotent()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewPersonAsync("FS");
        var schedule = await Service<ITimeScheduleService>().SaveAsync(FullSchedule("FS"), CancellationToken.None);
        var group = await NewGroupAsync("FS");
        await Service<IPersonnelAuthorizationService>().AssignGroupAsync(Membership(world, group), CancellationToken.None);
        await Service<IAccessGroupService>().SetDoorRuleAsync(group, world.DoorId, schedule.Id, true, CancellationToken.None);
        await CompleteOpenJobsAsync();

        var registry = new SimulatorControllerRegistry();
        var device = registry.Get(world.ControllerId);
        await device.ConnectAsync(new ControllerConnectionInfo { Host = "127.0.0.1", Port = 15050, ControllerCode = "SIM-ASC-01" }, CancellationToken.None);
        await device.UpsertCardAccessAsync(new ControllerCardAccess
        {
            CardNumber = "OLD" + world.CardNumber,
            Channels = [new ControllerChannelAccess { ChannelNumber = 1 }]
        }, CancellationToken.None);
        await device.DisconnectAsync(CancellationToken.None);

        await Service<IDeviceSyncService>().RequestFullSyncAsync(world.ControllerId, CancellationToken.None);
        await using (var auditDb = await Factory().CreateDbContextAsync())
        {
            Assert.Contains(await auditDb.AuditLogs.Where(log => log.EntityType == AuditEntities.DeviceSync).Select(log => log.Action).ToListAsync(), action => action == AuditActions.FullSyncRequested);
        }

        var clock = new FakeUtcClock { UtcNow = DateTime.UtcNow.AddHours(1) };
        var worker = Worker(clock, registry);
        await worker.ProcessBatchAsync(CancellationToken.None);
        var first = device.GetSnapshot();
        var card = Assert.Single(first.Cards, item => item.CardNumber == world.CardNumber);
        Assert.Equal([world.Channel], card.Channels.ToArray());
        Assert.DoesNotContain(first.Cards, item => item.CardNumber.StartsWith("OLD", StringComparison.Ordinal));

        await Service<IDeviceSyncService>().RequestFullSyncAsync(world.ControllerId, CancellationToken.None);
        await worker.ProcessBatchAsync(CancellationToken.None);
        var second = device.GetSnapshot();
        Assert.Equal(first.Cards.Select(item => item.CardNumber), second.Cards.Select(item => item.CardNumber));
        Assert.Equal(first.Cards.Single(item => item.CardNumber == world.CardNumber).Channels, second.Cards.Single(item => item.CardNumber == world.CardNumber).Channels);
    }

    [SkippableFact]
    public async Task Remove_succeeds_when_the_simulator_does_not_have_the_card()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewPersonAsync("RM");
        await Service<ICardService>().SetStatusAsync(world.CardId, CardStatus.Lost, CancellationToken.None);
        var job = await SinglePendingAsync(DeviceSyncOperations.RemoveCard, world.CardId.ToString());
        await IsolateAsync(job.Id);
        var registry = new SimulatorControllerRegistry();
        var clock = new FakeUtcClock { UtcNow = DateTime.UtcNow.AddHours(1) };
        await Worker(clock, registry).ProcessBatchAsync(CancellationToken.None);
        Assert.Equal(DeviceSyncStatus.Completed, (await ReloadAsync(job.Id)).Status);
        Assert.Empty(registry.Get(job.ControllerId).GetSnapshot().Cards);
    }

    [SkippableFact]
    public async Task Two_workers_do_not_process_the_same_job()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewPersonAsync("PW");
        var job = await SinglePendingAsync(DeviceSyncOperations.UpsertCard, world.CardId.ToString());
        await IsolateAsync(job.Id);
        var hold = new HoldController();
        var clock = new FakeUtcClock { UtcNow = DateTime.UtcNow.AddHours(1) };
        var options = Service<DeviceSyncOptions>();
        var processor = Service<IDeviceSyncProcessor>();
        var first = new DeviceSyncWorker(processor, hold, clock, options, NullLogger<DeviceSyncWorker>.Instance);
        var second = new DeviceSyncWorker(processor, hold, clock, options, NullLogger<DeviceSyncWorker>.Instance);
        var processing = first.ProcessBatchAsync(CancellationToken.None);
        await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var other = second.ProcessBatchAsync(CancellationToken.None);
        var finished = await Task.WhenAny(other, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.Same(other, finished);
        Assert.Equal(1, hold.Calls);
        hold.Release.TrySetResult();
        await processing;
        Assert.Equal(1, hold.Calls);
        Assert.Equal(DeviceSyncStatus.Completed, (await ReloadAsync(job.Id)).Status);
    }

    [SkippableFact]
    public async Task Development_host_uses_the_simulator_for_a_pending_card()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        await using var probe = await Factory().CreateDbContextAsync();
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:UnAccess"] = probe.Database.GetConnectionString()
        });
        builder.Services.AddSingleton<SimulatorControllerRegistry>();
        builder.Services.AddSingleton<IControllerClientFactory, SimulatorClientFactory>();
        builder.Services.AddUnAccessData(builder.Configuration);
        builder.Services.AddHostedService<DeviceSyncWorker>();
        using var host = builder.Build();
        var world = await NewPersonAsync("HS");
        var job = await SinglePendingAsync(DeviceSyncOperations.UpsertCard, world.CardId.ToString());
        await IsolateAsync(job.Id);
        var worker = host.Services.GetServices<IHostedService>().OfType<DeviceSyncWorker>().Single();
        await worker.ProcessBatchAsync(CancellationToken.None);
        var registry = host.Services.GetRequiredService<SimulatorControllerRegistry>();
        Assert.Contains(registry.Get(job.ControllerId).GetSnapshot().Cards, card => card.CardNumber == world.CardNumber);
    }

    [SkippableFact]
    public async Task Retry_failed_jobs_is_audited_once()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var world = await NewPersonAsync("RT");
        var job = await SinglePendingAsync(DeviceSyncOperations.UpsertCard, world.CardId.ToString());
        await IsolateAsync(job.Id);
        var registry = new SimulatorControllerRegistry();
        registry.Get(job.ControllerId).SetOffline(true);
        var clock = new FakeUtcClock { UtcNow = DateTime.UtcNow.AddHours(1) };
        await Worker(clock, registry).ProcessBatchAsync(CancellationToken.None);
        var count = await Service<IDeviceSyncService>().RetryFailedAsync(CancellationToken.None);
        Assert.True(count >= 1);
        Assert.Equal(DeviceSyncStatus.Pending, (await ReloadAsync(job.Id)).Status);
        await using var db = await Factory().CreateDbContextAsync();
        var audits = await db.AuditLogs.CountAsync(log => log.Action == AuditActions.RetryRequested && log.EntityType == AuditEntities.DeviceSync);
        Assert.True(audits >= 1);
    }

    private DeviceSyncWorker Worker(FakeUtcClock clock, SimulatorControllerRegistry registry) =>
        new(Service<IDeviceSyncProcessor>(), new SimulatorClientFactory(registry), clock, Service<DeviceSyncOptions>(), NullLogger<DeviceSyncWorker>.Instance);

    private async Task<World> NewPersonAsync(string prefix, bool withCard = true)
    {
        var personnel = Service<IPersonnelService>();
        var departments = await personnel.GetDepartmentsAsync(CancellationToken.None);
        var jobs = await personnel.GetJobTitlesAsync(CancellationToken.None);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var person = await personnel.CreateAsync(new PersonnelEditModel
        {
            PersonnelNumber = prefix + suffix,
            FirstName = "Senkron",
            LastName = "Test",
            DepartmentId = departments.Single(item => item.Code == "GC").Id,
            JobTitleId = jobs.Single(item => item.Code == "DOKTOR").Id,
            IsActive = true
        }, CancellationToken.None);
        var controllerId = await SimulatorIdAsync();
        var zoneId = await NewZoneAsync(prefix);
        var channel = NextChannel();
        var door = await Service<IHospitalStructureService>().CreateDoorAsync(Door(zoneId, "D", controllerId, channel), CancellationToken.None);
        if (!withCard)
            return new World(person.Id, 0, "", zoneId, door.Id, controllerId, channel, person);

        var number = prefix + suffix + "1";
        var card = await Service<ICardService>().CreateAsync(new CardEditModel
        {
            PersonnelId = person.Id,
            CardNumber = number,
            ValidFrom = new DateOnly(2026, 1, 1)
        }, CancellationToken.None);
        return new World(person.Id, card.Id, number, zoneId, door.Id, controllerId, channel, person);
    }

    private static int _nextChannel = 17;

    private static int NextChannel()
    {
        var channel = Interlocked.Increment(ref _nextChannel);
        if (channel == 90)
            channel = Interlocked.Increment(ref _nextChannel);
        return channel;
    }

    private async Task<int> NewZoneAsync(string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var hospital = Service<IHospitalStructureService>();
        var building = await hospital.CreateBuildingAsync(new BuildingEditModel { Code = "S" + suffix, Name = "Bina " + suffix, IsActive = true }, CancellationToken.None);
        var floor = await hospital.CreateFloorAsync(new FloorEditModel { BuildingId = building.Id, Code = "F", Name = "Kat", FloorNumber = 1, IsActive = true }, CancellationToken.None);
        var zone = await hospital.CreateZoneAsync(new ZoneEditModel { FloorId = floor.Id, Code = "Z", Name = "Bolge " + suffix, IsActive = true }, CancellationToken.None);
        return zone.Id;
    }

    private async Task<int> NewGroupAsync(string prefix)
    {
        var code = prefix + Guid.NewGuid().ToString("N")[..6];
        var group = await Service<IAccessGroupService>().SaveAsync(null, new AccessGroupEditModel { Code = code, Name = code, IsActive = true }, CancellationToken.None);
        return group.Id;
    }

    private static PersonnelGroupEditModel Membership(World world, int groupId) => new()
    {
        PersonnelId = world.PersonnelId,
        AccessGroupId = groupId,
        ValidFrom = new DateOnly(2026, 1, 1),
        IsActive = true
    };

    private static TimeScheduleEditModel FullSchedule(string prefix) => new()
    {
        Code = prefix + Guid.NewGuid().ToString("N")[..6],
        Name = "Plan " + prefix,
        IsActive = true,
        Intervals = Enum.GetValues<DayOfWeek>().Select(day => new TimeIntervalModel { DayOfWeek = day, StartTime = new TimeOnly(0, 0), EndTime = new TimeOnly(0, 0) }).ToList()
    };

    private static DoorEditModel Door(int zoneId, string prefix, int controllerId, int channel) => new()
    {
        ZoneId = zoneId,
        Code = prefix + Guid.NewGuid().ToString("N")[..6],
        Name = prefix,
        ControllerId = controllerId,
        ChannelNumber = channel,
        IsActive = true
    };

    private async Task<int> SimulatorIdAsync()
    {
        await using var db = await Factory().CreateDbContextAsync();
        return await db.Controllers.Where(item => item.Code == "SIM-ASC-01").Select(item => item.Id).SingleAsync();
    }

    private async Task<List<DeviceSyncQueue>> JobsAsync(string operation, string entityId)
    {
        await using var db = await Factory().CreateDbContextAsync();
        return await db.DeviceSyncQueue.Where(item => item.OperationType == operation && item.EntityId == entityId).ToListAsync();
    }

    private async Task<DeviceSyncQueue> SinglePendingAsync(string operation, string entityId)
    {
        var jobs = await JobsAsync(operation, entityId);
        return Assert.Single(jobs, job => job.Status == DeviceSyncStatus.Pending);
    }

    private async Task<DeviceSyncQueue> ReloadAsync(long id)
    {
        await using var db = await Factory().CreateDbContextAsync();
        return await db.DeviceSyncQueue.AsNoTracking().SingleAsync(item => item.Id == id);
    }

    private async Task CompleteJobsAsync(string operation, string entityId)
    {
        await using var db = await Factory().CreateDbContextAsync();
        var jobs = await db.DeviceSyncQueue.Where(item => item.OperationType == operation && item.EntityId == entityId && item.Status != DeviceSyncStatus.Completed).ToListAsync();
        foreach (var job in jobs)
            job.Status = DeviceSyncStatus.Completed;
        await db.SaveChangesAsync();
    }

    private async Task CompleteOpenJobsAsync()
    {
        await using var db = await Factory().CreateDbContextAsync();
        var jobs = await db.DeviceSyncQueue.Where(item => item.Status == DeviceSyncStatus.Pending || item.Status == DeviceSyncStatus.Failed || item.Status == DeviceSyncStatus.Processing).ToListAsync();
        foreach (var job in jobs)
            job.Status = DeviceSyncStatus.Completed;
        await db.SaveChangesAsync();
    }

    private async Task IsolateAsync(long jobId)
    {
        await using var db = await Factory().CreateDbContextAsync();
        var jobs = await db.DeviceSyncQueue
            .Where(item => item.Id != jobId && (item.Status == DeviceSyncStatus.Pending || item.Status == DeviceSyncStatus.Failed || item.Status == DeviceSyncStatus.Processing))
            .ToListAsync();
        foreach (var job in jobs)
            job.Status = DeviceSyncStatus.Completed;
        await db.SaveChangesAsync();
    }

    private T Service<T>() where T : notnull => _database.Provider!.GetRequiredService<T>();

    private IDbContextFactory<UnAccessDbContext> Factory() => _database.Provider!.GetRequiredService<IDbContextFactory<UnAccessDbContext>>();

    private sealed record World(int PersonnelId, int CardId, string CardNumber, int ZoneId, int DoorId, int ControllerId, int Channel, PersonnelDetail Person)
    {
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

    private sealed class FakeUtcClock : IUtcClock
    {
        public DateTime UtcNow { get; set; }
    }

    private sealed class FixedFactory : IDbContextFactory<UnAccessDbContext>
    {
        private readonly DbContextOptions<UnAccessDbContext> _options;

        public FixedFactory(DbContextOptions<UnAccessDbContext> options)
        {
            _options = options;
        }

        public UnAccessDbContext CreateDbContext() => new(_options);

        public Task<UnAccessDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private sealed class SecondSaveFails : SaveChangesInterceptor
    {
        private int _saves;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            _saves++;
            if (_saves >= 2)
                throw new InvalidOperationException("İkinci kayıt başarısız.");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class HoldController : IControllerClientFactory, IAccessController
    {
        public int Calls;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IAccessController Create(int controllerId) => this;

        public Task ConnectAsync(ControllerConnectionInfo connection, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<ControllerDoorInfo>> GetDoorsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ControllerDoorInfo>>([]);

        public Task AddOrUpdateCardAsync(ControllerCardRequest card, CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task UpsertCardAccessAsync(ControllerCardAccess access, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            Entered.TrySetResult();
            await Release.Task;
        }

        public Task RemoveCardAsync(string cardNumber, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ReplaceCardAccessAsync(IReadOnlyList<ControllerCardAccess> cards, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task OpenDoorAsync(int channelNumber, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<ControllerStatusInfo> GetStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ControllerStatusInfo { IsConnected = true });

        public Task StartEventListenerAsync(IAccessEventSink sink, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopEventListenerAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
