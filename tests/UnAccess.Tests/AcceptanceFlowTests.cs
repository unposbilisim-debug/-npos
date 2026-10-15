using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Entities;
using UnAccess.Core.Enums;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Core.Security;
using UnAccess.Core.Sync;
using UnAccess.Data;
using UnAccess.DeviceService;

namespace UnAccess.Tests;

[CollectionDefinition("acceptance")]
public sealed class AcceptanceCollection : ICollectionFixture<SqlFixture>
{
}

[Collection("acceptance")]
public sealed class AcceptanceFlowTests : IDisposable
{
    private const string AdminUsername = "kabul-admin";
    private const string AdminPassword = "KabulTest.1234";
    private const string IkUsername = "kabul-ik";
    private const string IkPassword = "KabulIk.12345";
    private readonly SqlFixture _database;

    public AcceptanceFlowTests(SqlFixture database) => _database = database;

    public void Dispose() => _database.Provider?.GetService<ICurrentUser>()?.SignOut();

    [SkippableFact]
    public async Task Service_acceptance_flow_covers_login_access_sync_report_and_restart()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var admin = await Service<IAccountService>().CreateInitialAdminAsync(new InitialAdminModel
        {
            Username = AdminUsername,
            FullName = "Kabul Yoneticisi",
            Password = AdminPassword
        }, CancellationToken.None);
        Assert.True(admin.Succeeded, admin.Message);
        Assert.Equal(AdminUsername, Service<ICurrentUser>().Username);
        Assert.Contains(RoleCodes.Admin, Service<ICurrentUser>().Roles);
        await OpenAdminSurfacesAsync();

        var personnel = Service<IPersonnelService>();
        var departments = await personnel.GetDepartmentsAsync(CancellationToken.None);
        var jobs = await personnel.GetJobTitlesAsync(CancellationToken.None);
        var person = await personnel.CreateAsync(new PersonnelEditModel
        {
            PersonnelNumber = "TEST001",
            FirstName = "Ahmet",
            LastName = "Test",
            DepartmentId = departments.Single(item => item.Code == "GC").Id,
            JobTitleId = jobs.Single(item => item.Code == "DOKTOR").Id,
            IsActive = true
        }, CancellationToken.None);
        var card = await Service<ICardService>().CreateAsync(new CardEditModel
        {
            PersonnelId = person.Id,
            CardNumber = "TESTCARD001",
            ValidFrom = new DateOnly(2026, 1, 1)
        }, CancellationToken.None);
        Assert.Equal(person.Id, card.PersonnelId);
        Assert.Equal(CardStatus.Active, card.Status);
        var owned = await Service<ICardService>().GetByPersonnelAsync(person.Id, CancellationToken.None);
        Assert.Contains(owned, item => item.CardNumber == "TESTCARD001" && item.PersonnelNumber == "TEST001");

        var door = await ServerDoorAsync();
        var controllerId = door.ControllerId;
        var schedule = (await Service<ITimeScheduleService>().ListAsync(CancellationToken.None)).Single(item => item.Code == "7_24");
        var group = await Service<IAccessGroupService>().SaveAsync(null, new AccessGroupEditModel
        {
            Code = "TEST-YETKI",
            Name = "Test Yetki",
            IsActive = true
        }, CancellationToken.None);
        await Service<IAccessGroupService>().SetDoorRuleAsync(group.Id, door.Id, schedule.Id, true, CancellationToken.None);
        await Service<IPersonnelAuthorizationService>().AssignGroupAsync(new PersonnelGroupEditModel
        {
            PersonnelId = person.Id,
            AccessGroupId = group.Id,
            ValidFrom = new DateOnly(2026, 1, 1),
            IsActive = true
        }, CancellationToken.None);

        var when = DateTime.UtcNow;
        var allowed = await Service<IAccessDecisionService>().EvaluateAsync(person.Id, door.Id, when, CancellationToken.None);
        Assert.True(allowed.IsAllowed);
        Assert.Equal("TESTCARD001", allowed.CardNumber);
        var granted = await Service<IPassageRecorder>().RecordAsync(person.Id, door.Id, when, CancellationToken.None);
        Assert.True(granted.Decision.IsAllowed);

        var queued = await Service<IDeviceSyncService>().SearchAsync(new DeviceSyncFilter(), CancellationToken.None);
        Assert.Contains(queued, item => item.EntityText == "Kart TESTCARD001" && item.Status == nameof(DeviceSyncStatus.Pending));
        var registry = new SimulatorControllerRegistry();
        var worker = Worker(registry);
        await DrainAsync(worker);
        var synced = registry.Get(controllerId).GetSnapshot();
        var syncedCard = Assert.Single(synced.Cards, item => item.CardNumber == "TESTCARD001");
        Assert.Contains(door.ChannelNumber, syncedCard.Channels);

        await Service<IPersonnelAuthorizationService>().RemoveGroupAsync(person.Id, group.Id, CancellationToken.None);
        var deniedDecision = await Service<IAccessDecisionService>().EvaluateAsync(person.Id, door.Id, when, CancellationToken.None);
        Assert.False(deniedDecision.IsAllowed);
        var denied = await Service<IPassageRecorder>().RecordAsync(person.Id, door.Id, when, CancellationToken.None);
        Assert.False(denied.Decision.IsAllowed);
        await DrainAsync(worker);
        var afterRevoke = registry.Get(controllerId).GetSnapshot();
        var revoked = afterRevoke.Cards.SingleOrDefault(item => item.CardNumber == "TESTCARD001");
        if (revoked != null)
            Assert.DoesNotContain(door.ChannelNumber, revoked.Channels);

        var live = await Service<IAccessEventQueryService>().GetRecentAsync(20, CancellationToken.None);
        var ours = live.Where(item => item.CardNumber == "TESTCARD001").ToList();
        Assert.Contains(ours, item => item.ResultText == "ONAYLANDI" && item.IsGranted && item.PersonnelName == "Ahmet Test" && item.DoorName == "Server Odası" && item.Reason.Length > 0 && item.TimeText.Length > 0);
        Assert.Contains(ours, item => item.ResultText == "REDDEDİLDİ" && !item.IsGranted && item.PersonnelName == "Ahmet Test" && item.DoorName == "Server Odası");

        var report = await Service<IReportService>().PassagesAsync(new PassageReportQuery { PersonnelId = person.Id, Page = 1, PageSize = 100 }, CancellationToken.None);
        Assert.Equal(2, report.TotalCount);
        Assert.Contains(report.Items, item => item.Decision == nameof(AccessDecisionKind.Granted));
        Assert.Contains(report.Items, item => item.Decision == nameof(AccessDecisionKind.Denied));
        await using var csvStream = new MemoryStream();
        await Service<IReportService>().WritePassagesCsvAsync(new PassageReportQuery { PersonnelId = person.Id }, csvStream, CancellationToken.None);
        var csv = Encoding.UTF8.GetString(csvStream.ToArray());
        Assert.Contains("TESTCARD001", csv, StringComparison.Ordinal);
        Assert.Contains(nameof(AccessDecisionKind.Granted), csv, StringComparison.Ordinal);
        Assert.Contains(nameof(AccessDecisionKind.Denied), csv, StringComparison.Ordinal);
        Assert.Contains("Server Odası", csv, StringComparison.Ordinal);

        var health = await Service<ISystemHealthService>().GetAsync(CancellationToken.None);
        Assert.True(health.DatabaseConnected);
        Assert.Equal("Bağlı", health.DatabaseText);
        Assert.Equal(HeartbeatStatus.Running, health.DeviceServiceText);
        Assert.NotNull(health.LastAccessEventUtc);
        Assert.Equal(0, health.SyncFailed);
        Assert.Equal(0, health.SyncDeadLetter);
        Assert.True(health.ControllersOnline >= 1);

        Service<ICurrentUser>().SignOut();
        var again = await Service<IAccountService>().LoginAsync(AdminUsername, AdminPassword, CancellationToken.None);
        Assert.True(again.Succeeded, again.Message);
        var restored = await Service<IPersonnelService>().SearchAsync(new PersonnelSearchCriteria { Text = "TEST001" }, CancellationToken.None);
        Assert.Contains(restored, item => item.PersonnelNumber == "TEST001" && item.ActiveCardNumber == "TESTCARD001");
        await using (var db = await Factory().CreateDbContextAsync())
        {
            var decisions = await db.AccessEvents.Where(item => item.CardNumber == "TESTCARD001").Select(item => item.Decision).ToListAsync();
            Assert.Contains(nameof(AccessDecisionKind.Granted), decisions);
            Assert.Contains(nameof(AccessDecisionKind.Denied), decisions);
            var audit = await db.AuditLogs.Where(item => item.UserId == again.Session!.UserId && item.Description.Contains("TEST001")).ToListAsync();
            Assert.NotEmpty(audit);
        }

        await Service<IUserAdminService>().CreateAsync(new UserWriteModel
        {
            Username = IkUsername,
            FullName = "Kabul Ik",
            Password = IkPassword,
            IsActive = true,
            RoleCodes = [RoleCodes.HumanResources]
        }, CancellationToken.None);
        Service<ICurrentUser>().SignOut();
        var ik = await Service<IAccountService>().LoginAsync(IkUsername, IkPassword, CancellationToken.None);
        Assert.True(ik.Succeeded, ik.Message);
        Assert.Equal([RoleCodes.HumanResources], ik.Session!.Roles);
        Assert.True(Service<IAccessGuard>().Can(Permissions.PersonnelView));
        Assert.True(Service<IAccessGuard>().Can(Permissions.CardsManage));
        Assert.False(Service<IAccessGuard>().Can(Permissions.AccessView));
        Assert.False(Service<IAccessGuard>().Can(Permissions.HospitalView));
        Assert.False(Service<IAccessGuard>().Can(Permissions.DevicesView));
        Assert.False(Service<IAccessGuard>().Can(Permissions.LiveView));
        Assert.False(Service<IAccessGuard>().Can(Permissions.ReportsView));
        Assert.False(Service<IAccessGuard>().Can(Permissions.UsersManage));
        Assert.False(Service<IAccessGuard>().Can(Permissions.SystemView));
        await Service<IPersonnelService>().SearchAsync(new PersonnelSearchCriteria { Text = "TEST001" }, CancellationToken.None);
        await Service<ICardService>().SearchAsync(new CardSearchCriteria { Text = "TESTCARD001" }, CancellationToken.None);
        await ExpectDenied(() => Service<IAccessGroupService>().ListAsync(CancellationToken.None));
        await ExpectDenied(() => Service<ITimeScheduleService>().ListAsync(CancellationToken.None));
        await ExpectDenied(() => Service<IPersonnelAuthorizationService>().GetAsync(person.Id, CancellationToken.None));
        await ExpectDenied(() => Service<IHospitalStructureService>().ListBuildingsAsync(CancellationToken.None));
        await ExpectDenied(() => Service<IHospitalStructureService>().ListDoorsAsync(null, CancellationToken.None));
        await ExpectDenied(() => Service<IControllerQueryService>().ListAsync(CancellationToken.None));
        await ExpectDenied(() => Service<IDeviceSyncService>().GetSummaryAsync(CancellationToken.None));
        await ExpectDenied(() => Service<IAccessEventQueryService>().GetRecentAsync(20, CancellationToken.None));
        await ExpectDenied(() => Service<IPassageRecorder>().RecordAsync(person.Id, door.Id, when, CancellationToken.None));
        await ExpectDenied(() => Service<IReportService>().PassagesAsync(new PassageReportQuery { PersonnelId = person.Id }, CancellationToken.None));
        await ExpectDenied(() => Service<IUserAdminService>().ListAsync(CancellationToken.None));
        await ExpectDenied(() => Service<IAuditQueryService>().SearchAsync(new AuditQuery(), CancellationToken.None));
        await ExpectDenied(() => Service<ISystemSettingService>().ListAsync(CancellationToken.None));
        await ExpectDenied(() => Service<ISystemHealthService>().GetAsync(CancellationToken.None));
    }

    [Fact]
    public void Menu_keys_map_to_a_screen_in_source()
    {
        var root = RepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "UnAccess.Desktop", "Forms", "MainForm.cs"));
        foreach (var key in new[]
        {
            "personnel", "cards", "access-groups", "schedules", "overrides",
            "buildings", "floors", "zones", "doors", "controllers", "sync",
            "simulator", "live", "report-passages", "report-denied", "report-personnel", "report-doors",
            "users", "audit", "settings", "health", "backup"
        })
        {
            Assert.Contains($"\"{key}\" =>", source, StringComparison.Ordinal);
        }
    }

    private async Task OpenAdminSurfacesAsync()
    {
        Assert.Equal(Permissions.All.Length, Service<ICurrentUser>().Permissions.Count);
        await Service<IPersonnelService>().SearchAsync(new PersonnelSearchCriteria(), CancellationToken.None);
        await Service<ICardService>().SearchAsync(new CardSearchCriteria(), CancellationToken.None);
        await Service<IAccessGroupService>().ListAsync(CancellationToken.None);
        await Service<ITimeScheduleService>().ListAsync(CancellationToken.None);
        await Service<IHospitalStructureService>().ListBuildingsAsync(CancellationToken.None);
        await Service<IHospitalStructureService>().ListFloorsAsync(null, CancellationToken.None);
        await Service<IHospitalStructureService>().ListZonesAsync(null, null, CancellationToken.None);
        await Service<IHospitalStructureService>().ListDoorsAsync(null, CancellationToken.None);
        await Service<IControllerQueryService>().ListAsync(CancellationToken.None);
        await Service<IDeviceSyncService>().GetSummaryAsync(CancellationToken.None);
        await Service<IAccessEventQueryService>().GetRecentAsync(20, CancellationToken.None);
        await Service<IReportService>().PassagesAsync(new PassageReportQuery(), CancellationToken.None);
        await Service<IUserAdminService>().ListAsync(CancellationToken.None);
        await Service<IAuditQueryService>().SearchAsync(new AuditQuery(), CancellationToken.None);
        await Service<ISystemSettingService>().ListAsync(CancellationToken.None);
        await Service<ISystemHealthService>().GetAsync(CancellationToken.None);
    }

    private async Task<DoorRef> ServerDoorAsync()
    {
        var doors = await Service<IHospitalStructureService>().ListDoorsAsync(null, CancellationToken.None);
        var listed = doors.Single(item => item.Code == "SERVER");
        Assert.True(listed.IsActive);
        Assert.Equal("Server Odası", listed.Name);
        Assert.Equal(15, listed.ChannelNumber);
        var door = await Service<IHospitalStructureService>().GetDoorAsync(listed.Id, CancellationToken.None);
        Assert.NotNull(door);
        Assert.True(door!.IsActive);
        Assert.NotNull(door.ControllerId);
        await using var db = await Factory().CreateDbContextAsync();
        var controller = await db.Controllers.SingleAsync(item => item.Id == door.ControllerId);
        Assert.Equal("SIM-ASC-01", controller.Code);
        Assert.True(controller.IsSimulation);
        Assert.True(controller.IsActive);
        return new DoorRef(listed.Id, door.ControllerId.Value, listed.ChannelNumber);
    }

    private sealed record DoorRef(int Id, int ControllerId, int ChannelNumber);

    private DeviceSyncWorker Worker(SimulatorControllerRegistry registry) =>
        new(
            Service<IDeviceSyncProcessor>(),
            new SimulatorClientFactory(registry),
            Service<IUtcClock>(),
            Service<DeviceSyncOptions>(),
            NullLogger<DeviceSyncWorker>.Instance,
            Service<IRuntimeSettings>(),
            Service<IServiceMonitor>());

    private async Task DrainAsync(DeviceSyncWorker worker)
    {
        string stuck = "";
        for (var attempt = 0; attempt < 8; attempt++)
        {
            await worker.ProcessBatchAsync(CancellationToken.None);
            var summary = await Service<IDeviceSyncService>().GetSummaryAsync(CancellationToken.None);
            if (summary.Pending == 0 && summary.Processing == 0 && summary.Failed == 0)
                return;
            var rows = await Service<IDeviceSyncService>().SearchAsync(new DeviceSyncFilter(), CancellationToken.None);
            stuck = string.Join("; ", rows.Where(item => item.Status != nameof(DeviceSyncStatus.Completed)).Select(item => $"{item.OperationText} {item.StatusText} {item.LastError}"));
        }

        Assert.Fail(stuck);
    }

    private static async Task ExpectDenied(Func<Task> call)
    {
        var exception = await Assert.ThrowsAsync<UnAccessException>(call);
        Assert.Equal("Bu işlem için yetkiniz yok.", exception.UserMessage);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "UnAccess.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Çözüm kökü bulunamadı.");
    }

    private T Service<T>() where T : notnull => _database.Provider!.GetRequiredService<T>();

    private IDbContextFactory<UnAccessDbContext> Factory() => Service<IDbContextFactory<UnAccessDbContext>>();
}
