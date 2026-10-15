using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Audit;
using UnAccess.Core.Entities;
using UnAccess.Core.Enums;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Core.Security;
using UnAccess.Data;
using UnAccess.Data.Infrastructure;
using UnAccess.Data.Services;

namespace UnAccess.Tests;

[Collection("sql")]
public class SecuritySqlTests : IDisposable
{
    private const string AdminPassword = "Yonetici.1234";
    private const string UserPassword = "Kullanici.1234";
    private static string? _adminUsername;
    private readonly SqlFixture _database;

    public SecuritySqlTests(SqlFixture database) => _database = database;

    public void Dispose() => _database.Provider?.GetService<ICurrentUser>()?.SignOut();

    [SkippableFact]
    public async Task Correct_password_logs_in_and_hash_is_not_plaintext()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var username = await NewUserAsync("hash");
        Service<ICurrentUser>().SignOut();
        var result = await Service<IAccountService>().LoginAsync(username, UserPassword, CancellationToken.None);
        Assert.True(result.Succeeded);
        await using var db = await Factory().CreateDbContextAsync();
        var hash = await db.Users.Where(user => user.Username == username).Select(user => user.PasswordHash).SingleAsync();
        Assert.NotEqual(UserPassword, hash);
        Assert.DoesNotContain(UserPassword, hash, StringComparison.Ordinal);
        Service<ICurrentUser>().SignOut();
    }

    [SkippableFact]
    public async Task Wrong_password_is_rejected_with_a_generic_message()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var username = await NewUserAsync("bad");
        Service<ICurrentUser>().SignOut();
        var result = await Service<IAccountService>().LoginAsync(username, "YanlisSifre.99", CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Equal(LoginMessages.InvalidCredentials, result.Message);
        var missing = await Service<IAccountService>().LoginAsync("yok-" + Guid.NewGuid().ToString("N")[..8], UserPassword, CancellationToken.None);
        Assert.Equal(LoginMessages.InvalidCredentials, missing.Message);
    }

    [SkippableFact]
    public async Task Five_failures_lock_the_account_until_the_clock_passes()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var username = await NewUserAsync("lock");
        Service<ICurrentUser>().SignOut();
        var clock = new FakeUtcClock { UtcNow = DateTime.UtcNow };
        var accounts = Clocked(clock);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await accounts.LoginAsync(username, "YanlisSifre.99", CancellationToken.None);
            Assert.False(failed.Succeeded);
        }

        var locked = await accounts.LoginAsync(username, UserPassword, CancellationToken.None);
        Assert.False(locked.Succeeded);
        Assert.Equal(LoginMessages.Locked, locked.Message);

        await using (var db = await Factory().CreateDbContextAsync())
        {
            var until = await db.Users.Where(user => user.Username == username).Select(user => user.LockoutUntilUtc).SingleAsync();
            Assert.NotNull(until);
            clock.UtcNow = until.Value.AddSeconds(-1);
        }

        var stillLocked = await accounts.LoginAsync(username, UserPassword, CancellationToken.None);
        Assert.Equal(LoginMessages.Locked, stillLocked.Message);
        clock.UtcNow = clock.UtcNow.AddSeconds(2);
        var opened = await accounts.LoginAsync(username, UserPassword, CancellationToken.None);
        Assert.True(opened.Succeeded);
        await using var after = await Factory().CreateDbContextAsync();
        var stored = await after.Users.SingleAsync(user => user.Username == username);
        Assert.Equal(0, stored.FailedLoginCount);
        Assert.Null(stored.LockoutUntilUtc);
        Service<ICurrentUser>().SignOut();
    }

    [SkippableFact]
    public async Task Successful_login_resets_the_failure_count()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var username = await NewUserAsync("resetc");
        Service<ICurrentUser>().SignOut();
        var clock = new FakeUtcClock { UtcNow = DateTime.UtcNow };
        var accounts = Clocked(clock);
        await accounts.LoginAsync(username, "YanlisSifre.99", CancellationToken.None);
        await accounts.LoginAsync(username, "YanlisSifre.99", CancellationToken.None);
        var result = await accounts.LoginAsync(username, UserPassword, CancellationToken.None);
        Assert.True(result.Succeeded);
        await using var db = await Factory().CreateDbContextAsync();
        Assert.Equal(0, await db.Users.Where(user => user.Username == username).Select(user => user.FailedLoginCount).SingleAsync());
        Service<ICurrentUser>().SignOut();
    }

    [SkippableFact]
    public async Task Inactive_user_cannot_log_in()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var username = await NewUserAsync("pasif");
        await using (var db = await Factory().CreateDbContextAsync())
        {
            var user = await db.Users.SingleAsync(item => item.Username == username);
            await Service<IUserAdminService>().SetActiveAsync(user.Id, false, CancellationToken.None);
        }

        Service<ICurrentUser>().SignOut();
        var result = await Service<IAccountService>().LoginAsync(username, UserPassword, CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Equal(LoginMessages.InvalidCredentials, result.Message);
    }

    [SkippableFact]
    public async Task Created_user_must_change_password_and_then_the_flag_clears()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var username = await NewUserAsync("degis");
        Service<ICurrentUser>().SignOut();
        var first = await Service<IAccountService>().LoginAsync(username, UserPassword, CancellationToken.None);
        Assert.True(first.MustChangePassword);
        await Service<IAccountService>().ChangeOwnPasswordAsync(UserPassword, "YeniSifre.1234", CancellationToken.None);
        Assert.False(Service<ICurrentUser>().MustChangePassword);
        Service<ICurrentUser>().SignOut();
        var second = await Service<IAccountService>().LoginAsync(username, "YeniSifre.1234", CancellationToken.None);
        Assert.True(second.Succeeded);
        Assert.False(second.MustChangePassword);
        Service<ICurrentUser>().SignOut();
    }

    [SkippableFact]
    public async Task Last_admin_cannot_be_deactivated_or_lose_the_role_and_a_user_cannot_deactivate_self()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        await EnsureAdminAsync();
        var adminId = Service<ICurrentUser>().UserId!.Value;
        var deactivate = await Assert.ThrowsAsync<UnAccessException>(() => Service<IUserAdminService>().SetActiveAsync(adminId, false, CancellationToken.None));
        Assert.Equal("Son aktif yönetici pasife alınamaz.", deactivate.UserMessage);
        var remove = await Assert.ThrowsAsync<UnAccessException>(() => Service<IUserAdminService>().UpdateAsync(adminId, new UserWriteModel
        {
            Username = _adminUsername!,
            FullName = "Sistem Yoneticisi",
            IsActive = true,
            RoleCodes = [RoleCodes.HumanResources]
        }, CancellationToken.None));
        Assert.Equal("Son yönetici rolü kaldırılamaz.", remove.UserMessage);

        var second = "ikinci" + Guid.NewGuid().ToString("N")[..6];
        await Service<IUserAdminService>().CreateAsync(new UserWriteModel
        {
            Username = second,
            FullName = "Ikinci Yonetici",
            Password = UserPassword,
            IsActive = true,
            RoleCodes = [RoleCodes.Admin]
        }, CancellationToken.None);
        Service<ICurrentUser>().SignOut();
        var login = await Service<IAccountService>().LoginAsync(second, UserPassword, CancellationToken.None);
        Assert.True(login.Succeeded);
        var self = await Assert.ThrowsAsync<UnAccessException>(() => Service<IUserAdminService>().SetActiveAsync(login.Session!.UserId, false, CancellationToken.None));
        Assert.Equal("Kendi hesabınızı pasife alamazsınız.", self.UserMessage);
        Service<ICurrentUser>().SignOut();
    }

    [SkippableFact]
    public async Task Audit_uses_the_signed_in_user_and_stays_empty_without_a_session()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        Service<ICurrentUser>().SignOut();
        await Service<IAuditService>().WriteAsync(new AuditEntry
        {
            Action = AuditActions.Updated,
            EntityType = "Background",
            EntityId = "0",
            Description = "Arka plan denetimi " + Guid.NewGuid().ToString("N")
        }, CancellationToken.None);
        await using (var db = await Factory().CreateDbContextAsync())
        {
            var background = await db.AuditLogs.OrderByDescending(item => item.Id).FirstAsync(item => item.EntityType == "Background");
            Assert.Null(background.UserId);
        }

        await EnsureAdminAsync();
        var userId = Service<ICurrentUser>().UserId;
        await Service<IAuditService>().WriteAsync(new AuditEntry
        {
            Action = AuditActions.Updated,
            EntityType = AuditEntities.Personnel,
            EntityId = "oturum",
            Description = "Oturumlu denetim " + Guid.NewGuid().ToString("N")
        }, CancellationToken.None);
        await using var after = await Factory().CreateDbContextAsync();
        var logged = await after.AuditLogs.OrderByDescending(item => item.Id).FirstAsync(item => item.EntityId == "oturum");
        Assert.Equal(userId, logged.UserId);
        Service<ICurrentUser>().SignOut();
    }

    [SkippableFact]
    public async Task Login_security_events_do_not_store_the_password()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var username = await NewUserAsync("sec");
        const string wrong = "GizliSifre.99";
        Service<ICurrentUser>().SignOut();
        await Service<IAccountService>().LoginAsync(username, wrong, CancellationToken.None);
        await Service<IAccountService>().LoginAsync(username, UserPassword, CancellationToken.None);
        await using var db = await Factory().CreateDbContextAsync();
        var events = await db.SecurityEvents.Where(item => item.Username == username).ToListAsync();
        Assert.Contains(events, item => item.EventType == SecurityEventTypes.LoginFailed && !item.Success);
        Assert.Contains(events, item => item.EventType == SecurityEventTypes.LoginSuccess && item.Success);
        foreach (var item in events)
        {
            Assert.DoesNotContain(wrong, item.Detail ?? "", StringComparison.Ordinal);
            Assert.DoesNotContain(UserPassword, item.Detail ?? "", StringComparison.Ordinal);
            Assert.DoesNotContain(wrong, item.Username, StringComparison.Ordinal);
            Assert.DoesNotContain(UserPassword, item.EventType, StringComparison.Ordinal);
        }

        Service<ICurrentUser>().SignOut();
    }

    [SkippableFact]
    public async Task Heartbeat_is_online_until_it_becomes_stale()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var instance = Guid.NewGuid().ToString("N");
        await Service<IServiceMonitor>().BeatAsync(new ServicePulse
        {
            ServiceName = ServiceNames.DeviceSync,
            InstanceId = instance,
            StartedAtUtc = DateTime.UtcNow,
            Version = "1.0.0"
        }, CancellationToken.None);
        var alive = await Service<ISystemHealthService>().GetAsync(CancellationToken.None);
        Assert.Equal(HeartbeatStatus.Running, alive.DeviceServiceText);

        await using (var db = await Factory().CreateDbContextAsync())
        {
            var rows = await db.ServiceHeartbeats.ToListAsync();
            foreach (var row in rows)
                row.LastSeenAtUtc = DateTime.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync();
        }

        var stale = await Service<ISystemHealthService>().GetAsync(CancellationToken.None);
        Assert.Equal(HeartbeatStatus.NotResponding, stale.DeviceServiceText);
        Assert.True(HeartbeatStatus.IsAlive(DateTime.UtcNow, DateTime.UtcNow, TimeSpan.FromSeconds(90)));
        Assert.False(HeartbeatStatus.IsAlive(DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow, TimeSpan.FromSeconds(90)));
    }

    [SkippableFact]
    public async Task Passage_report_pages_filters_and_exports_the_same_filter()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var first = await CreatePersonAsync("RA");
        var second = await CreatePersonAsync("RB");
        await using var db = await Factory().CreateDbContextAsync();
        var doors = await db.Doors.AsNoTracking().OrderBy(door => door.Id).Take(2).ToListAsync();
        Assert.True(doors.Count >= 2);
        var marker = "R" + Guid.NewGuid().ToString("N")[..8];
        var rows = new List<AccessEvent>();
        for (var index = 0; index < 51; index++)
        {
            rows.Add(Event(first, doors[0].Id, marker + "-A", index < 50 ? nameof(AccessDecisionKind.Granted) : nameof(AccessDecisionKind.Denied), index));
        }

        rows.Add(Event(second, doors[1].Id, marker + "-B", nameof(AccessDecisionKind.Granted), 100));
        db.AccessEvents.AddRange(rows);
        await db.SaveChangesAsync();

        var reports = Service<IReportService>();
        var page = await reports.PassagesAsync(new PassageReportQuery { PersonnelId = first, Page = 1, PageSize = 50 }, CancellationToken.None);
        Assert.Equal(51, page.TotalCount);
        Assert.Equal(50, page.Items.Count);
        var next = await reports.PassagesAsync(new PassageReportQuery { PersonnelId = first, Page = 2, PageSize = 50 }, CancellationToken.None);
        Assert.Single(next.Items);

        var denied = await reports.PassagesAsync(new PassageReportQuery { PersonnelId = first, DeniedOnly = true, Page = 1, PageSize = 50 }, CancellationToken.None);
        Assert.Equal(1, denied.TotalCount);
        Assert.All(denied.Items, item => Assert.Equal(nameof(AccessDecisionKind.Denied), item.Decision));

        var byPerson = await reports.PassagesAsync(new PassageReportQuery { PersonnelId = second, Page = 1, PageSize = 50 }, CancellationToken.None);
        Assert.Single(byPerson.Items);
        Assert.Equal(marker + "-B", byPerson.Items[0].CardNumber);

        var byDoor = await reports.PassagesAsync(new PassageReportQuery { DoorId = doors[0].Id, PersonnelId = first, Page = 1, PageSize = 100 }, CancellationToken.None);
        Assert.Equal(51, byDoor.TotalCount);
        Assert.All(byDoor.Items, item => Assert.Equal(marker + "-A", item.CardNumber));

        await using var stream = new MemoryStream();
        await reports.WritePassagesCsvAsync(new PassageReportQuery { PersonnelId = second }, stream, CancellationToken.None);
        var csv = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains(marker + "-B", csv, StringComparison.Ordinal);
        Assert.DoesNotContain(marker + "-A", csv, StringComparison.Ordinal);
    }

    private async Task<int> CreatePersonAsync(string prefix)
    {
        Service<ICurrentUser>().SignOut();
        var personnel = Service<IPersonnelService>();
        var departments = await personnel.GetDepartmentsAsync(CancellationToken.None);
        var jobs = await personnel.GetJobTitlesAsync(CancellationToken.None);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var person = await personnel.CreateAsync(new PersonnelEditModel
        {
            PersonnelNumber = prefix + suffix,
            FirstName = "Rapor",
            LastName = prefix,
            DepartmentId = departments.Single(item => item.Code == "GC").Id,
            JobTitleId = jobs.Single(item => item.Code == "DOKTOR").Id,
            IsActive = true
        }, CancellationToken.None);
        return person.Id;
    }

    private async Task<string> NewUserAsync(string prefix)
    {
        await EnsureAdminAsync();
        var username = prefix + Guid.NewGuid().ToString("N")[..8];
        await Service<IUserAdminService>().CreateAsync(new UserWriteModel
        {
            Username = username,
            FullName = "Test Kullanici",
            Password = UserPassword,
            IsActive = true,
            RoleCodes = [RoleCodes.Observer]
        }, CancellationToken.None);
        return username;
    }

    private async Task EnsureAdminAsync()
    {
        var accounts = Service<IAccountService>();
        var current = Service<ICurrentUser>();
        if (_adminUsername == null)
        {
            if (!await accounts.HasAnyUserAsync(CancellationToken.None))
            {
                _adminUsername = "yonetici";
                var created = await accounts.CreateInitialAdminAsync(new InitialAdminModel
                {
                    Username = _adminUsername,
                    FullName = "Sistem Yoneticisi",
                    Password = AdminPassword
                }, CancellationToken.None);
                Assert.True(created.Succeeded);
                return;
            }
        }

        current.SignOut();
        var login = await accounts.LoginAsync(_adminUsername ?? "yonetici", AdminPassword, CancellationToken.None);
        Assert.True(login.Succeeded, login.Message);
        _adminUsername ??= login.Session!.Username;
    }

    private AccountService Clocked(FakeUtcClock clock) => new(
        Factory(),
        Service<IPasswordHasher>(),
        clock,
        Service<ICurrentUser>(),
        Service<IRuntimeSettings>(),
        Service<AuditWriter>());

    private static AccessEvent Event(int personnelId, int doorId, string cardNumber, string decision, int offset) => new()
    {
        EventId = Guid.NewGuid().ToString("N"),
        DoorId = doorId,
        PersonnelId = personnelId,
        CardNumber = cardNumber,
        EventType = AccessEventTypes.CardRead,
        Decision = decision,
        Reason = decision,
        OccurredAtUtc = DateTime.UtcNow.AddMinutes(-offset),
        ReceivedAtUtc = DateTime.UtcNow,
        Source = AccessEventSources.Simulator
    };

    private T Service<T>() where T : notnull => _database.Provider!.GetRequiredService<T>();

    private IDbContextFactory<UnAccessDbContext> Factory() => Service<IDbContextFactory<UnAccessDbContext>>();

    private sealed class FakeUtcClock : IUtcClock
    {
        public DateTime UtcNow { get; set; }
    }
}
