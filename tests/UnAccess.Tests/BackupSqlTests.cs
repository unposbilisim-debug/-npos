using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Audit;
using UnAccess.Core.Backup;
using UnAccess.Core.Entities;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Core.Security;
using UnAccess.Core.Sync;
using UnAccess.Data;
using UnAccess.Data.Infrastructure;
using UnAccess.Data.Services;
using UnAccess.DeviceService;
using UnAccess.Devices;

namespace UnAccess.Tests;

[CollectionDefinition("backup-sql")]
public sealed class BackupSqlCollection : ICollectionFixture<BackupSqlFixture>
{
}

public sealed class BackupSqlFixture : IAsyncLifetime
{
    public bool Available { get; private set; }
    public string SkipReason { get; private set; } = "SQL Server kullanılamıyor.";
    public ServiceProvider? Provider { get; private set; }
    public string Catalog { get; private set; } = "";
    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__UnAccess");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            SkipReason = "ConnectionStrings__UnAccess tanımlı değil.";
            return;
        }

        try
        {
            Catalog = "UnAccess_" + Guid.NewGuid().ToString("N");
            if (string.Equals(Catalog, "UnAccessDB", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Test veritabanı üretim adını kullanamaz.");
            var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = Catalog };
            ConnectionString = builder.ConnectionString;
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:UnAccess"] = ConnectionString,
                    ["Database:AutoMigrate"] = "true",
                    ["Seed:Enabled"] = "true"
                })
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
            services.AddUnAccessData(configuration);
            Provider = services.BuildServiceProvider();
            await Provider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
            Available = true;
        }
        catch (Exception exception)
        {
            SkipReason = exception.GetBaseException().Message;
            Available = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (Provider != null)
        {
            await Provider.DisposeAsync();
            if (Available)
                await BackupSqlSupport.DropAsync(ConnectionString, Catalog);
        }
    }
}

[Collection("backup-sql")]
public sealed class BackupSqlTests
{
    private const string AdminUser = "phase5admin";
    private const string AdminPassword = "Phase5-Admin-1";
    private readonly BackupSqlFixture _database;

    public BackupSqlTests(BackupSqlFixture database) => _database = database;

    [SkippableFact]
    public async Task Manual_backup_succeeds()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        await SignInAsync();
        var directory = BackupSqlSupport.Directory();
        await PutAsync(SettingKeys.BackupDirectory, directory);
        var result = await Service<IBackupService>().CreateManualAsync(CancellationToken.None);
        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(BackupMessages.Succeeded, result.Message);
        Assert.Matches("^[A-Za-z_][A-Za-z0-9_]*_[0-9]{8}_[0-9]{6}(_[0-9]+)?\\.bak$", result.FileName);
        Assert.NotEqual("UnAccessDB", _database.Catalog);
        await Service<SqlBackupGateway>().VerifyAsync(result.FullPath!, CancellationToken.None);
    }

    [SkippableFact]
    public async Task Backup_history_is_completed()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        await SignInAsync();
        var directory = BackupSqlSupport.Directory();
        await PutAsync(SettingKeys.BackupDirectory, directory);
        var result = await Service<IBackupService>().CreateManualAsync(CancellationToken.None);
        await using var db = await Factory().CreateDbContextAsync();
        var row = await db.BackupHistories.SingleAsync(item => item.FileName == result.FileName && item.BackupDirectory == directory);
        Assert.Equal(BackupStatuses.Completed, row.Status);
        Assert.Equal(BackupKinds.Manual, row.BackupType);
        Assert.NotNull(row.CompletedAtUtc);
        Assert.Equal(BackupTriggers.User, row.TriggeredBy);
    }

    [SkippableFact]
    public async Task Failed_backup_is_recorded()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        await SignInAsync();
        await PutAsync(SettingKeys.BackupDirectory, BackupSqlSupport.UnwritableDirectory());
        var exception = await Assert.ThrowsAsync<UnAccessException>(() => Service<IBackupService>().CreateManualAsync(CancellationToken.None));
        Assert.Equal(BackupMessages.BackupPathFailed, exception.UserMessage);
        await using var db = await Factory().CreateDbContextAsync();
        var row = await db.BackupHistories.OrderByDescending(item => item.Id).FirstAsync();
        Assert.Equal(BackupStatuses.Failed, row.Status);
        Assert.Equal(BackupMessages.BackupPathFailed, row.ErrorMessage);
    }

    [SkippableFact]
    public async Task Backup_permission_error_is_understandable()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var login = "ua_nobackup_" + Guid.NewGuid().ToString("N")[..12];
        var directory = BackupSqlSupport.Directory();
        var path = BackupFiles.Join(directory, "permission.bak");
        try
        {
            await BackupSqlSupport.ExecuteAsync(_database.ConnectionString, "DECLARE @sql nvarchar(max) = N'CREATE LOGIN ' + QUOTENAME(@login) + N' WITH PASSWORD = ' + QUOTENAME(@password, '''') + N', CHECK_POLICY = OFF'; EXEC(@sql);", ("@login", login), ("@password", "BackupDenied-User-1"));
            await BackupSqlSupport.ExecuteAsync(_database.ConnectionString, "CREATE USER [" + login + "] FOR LOGIN [" + login + "]");
            var limited = new SqlConnectionStringBuilder(_database.ConnectionString)
            {
                UserID = login,
                Password = "BackupDenied-User-1"
            };
            var exception = await Assert.ThrowsAsync<UnAccessException>(() =>
                Service<SqlBackupGateway>().BackupDatabaseAsync(limited.ConnectionString, _database.Catalog, path, CancellationToken.None));
            Assert.Equal(BackupMessages.PermissionDenied, exception.UserMessage);
            Assert.DoesNotContain("BackupDenied-User-1", exception.UserMessage, StringComparison.Ordinal);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await BackupSqlSupport.ExecuteAsync(BackupSqlSupport.Master(_database.ConnectionString), """
                DECLARE @kill nvarchar(max) = N'';
                SELECT @kill = @kill + N'KILL ' + CONVERT(nvarchar(11), session_id) + N';'
                FROM sys.dm_exec_sessions WHERE login_name = @login AND session_id <> @@SPID;
                IF LEN(@kill) > 0 EXEC(@kill);
                """, ("@login", login));
            await BackupSqlSupport.ExecuteAsync(_database.ConnectionString, "IF USER_ID(@login) IS NOT NULL BEGIN DECLARE @sql nvarchar(max) = N'DROP USER ' + QUOTENAME(@login); EXEC(@sql); END", ("@login", login));
            await BackupSqlSupport.ExecuteAsync(BackupSqlSupport.Master(_database.ConnectionString), "IF SUSER_ID(@login) IS NOT NULL BEGIN DECLARE @sql nvarchar(max) = N'DROP LOGIN ' + QUOTENAME(@login); EXEC(@sql); END", ("@login", login));
        }
    }

    [SkippableFact]
    public async Task Two_backups_cannot_run_together_and_a_released_lock_can_be_taken_again()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        await SignInAsync();
        await PutAsync(SettingKeys.BackupDirectory, BackupSqlSupport.Directory());
        var gate = Service<IMaintenanceGate>();
        var held = await gate.TryEnterExclusiveAsync(CancellationToken.None);
        Assert.NotNull(held);
        var blocked = await gate.TryEnterExclusiveAsync(CancellationToken.None);
        Assert.Null(blocked);
        var exception = await Assert.ThrowsAsync<UnAccessException>(() => Service<IBackupService>().CreateManualAsync(CancellationToken.None));
        Assert.Equal(BackupMessages.Busy, exception.UserMessage);
        await held.DisposeAsync();
        var recovered = await gate.TryEnterExclusiveAsync(CancellationToken.None);
        Assert.NotNull(recovered);
        await recovered.DisposeAsync();
    }

    [SkippableFact]
    public async Task Scheduled_backup_runs_when_due()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        await ClearScheduledAsync();
        var directory = BackupSqlSupport.Directory();
        await PutAsync(SettingKeys.BackupDirectory, directory);
        await PutAsync(SettingKeys.BackupEnabled, "true");
        await PutAsync(SettingKeys.BackupTime, "00:00");
        var before = await CountScheduledAsync();
        var result = await Service<IBackupService>().CreateScheduledAsync(CancellationToken.None);
        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.FileName);
        Assert.Equal(before + 1, await CountScheduledAsync());
        await using var db = await Factory().CreateDbContextAsync();
        var row = await db.BackupHistories.SingleAsync(item => item.FileName == result.FileName && item.BackupDirectory == directory);
        Assert.Equal(BackupKinds.Scheduled, row.BackupType);
        Assert.Equal(BackupStatuses.Completed, row.Status);
        Assert.Equal(BackupTriggers.DeviceService, row.TriggeredBy);
    }

    [SkippableFact]
    public async Task Second_scheduled_backup_does_not_run_the_same_day()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        await PutAsync(SettingKeys.BackupDirectory, BackupSqlSupport.Directory());
        await PutAsync(SettingKeys.BackupEnabled, "true");
        await PutAsync(SettingKeys.BackupTime, "00:00");
        var first = await Service<IBackupService>().CreateScheduledAsync(CancellationToken.None);
        if (first.FileName == null)
            first = await ForceOneScheduledAsync();
        var count = await CountScheduledAsync();
        var second = await Service<IBackupService>().CreateScheduledAsync(CancellationToken.None);
        Assert.Null(second.FileName);
        Assert.Equal(count, await CountScheduledAsync());
    }

    [SkippableFact]
    public async Task Missed_scheduled_backup_runs_once_when_the_service_starts()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        await ClearScheduledAsync();
        await PutAsync(SettingKeys.BackupDirectory, BackupSqlSupport.Directory());
        await PutAsync(SettingKeys.BackupEnabled, "true");
        await PutAsync(SettingKeys.BackupTime, "00:00");
        await using (var db = await Factory().CreateDbContextAsync())
        {
            db.BackupHistories.Add(new BackupHistory
            {
                BackupType = BackupKinds.Scheduled,
                FileName = _database.Catalog + "_20200101_020000.bak",
                BackupDirectory = "/old",
                StartedAtUtc = DateTime.UtcNow.AddDays(-3),
                CompletedAtUtc = DateTime.UtcNow.AddDays(-3),
                Status = BackupStatuses.Completed,
                TriggeredBy = BackupTriggers.DeviceService
            });
            await db.SaveChangesAsync();
        }

        var worker = new BackupSchedulerWorker(Service<IBackupService>(), NullLogger<BackupSchedulerWorker>.Instance);
        await worker.RunOnceAsync(CancellationToken.None);
        var after = await CountScheduledAsync();
        Assert.Equal(2, after);
        await worker.RunOnceAsync(CancellationToken.None);
        Assert.Equal(after, await CountScheduledAsync());
    }

    [SkippableFact]
    public async Task Retention_deletes_only_matching_scheduled_backups()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var directory = BackupSqlSupport.Directory();
        await PutAsync(SettingKeys.BackupDirectory, directory);
        await PutAsync(SettingKeys.BackupRetentionDays, "30");
        var scheduled = _database.Catalog + "_20200101_020000.bak";
        var recent = _database.Catalog + "_20261014_020000.bak";
        var manual = _database.Catalog + "_20200101_030000.bak";
        var preRestore = _database.Catalog + "_PreRestore_20200101_040000.bak";
        var stranger = "notes.bak";
        var untracked = _database.Catalog + "_20200102_020000.bak";
        foreach (var name in new[] { scheduled, recent, manual, preRestore, stranger, untracked })
            await File.WriteAllTextAsync(Path.Combine(directory, name), "bak");

        await using (var db = await Factory().CreateDbContextAsync())
        {
            db.BackupHistories.AddRange(
                History(BackupKinds.Scheduled, scheduled, directory, DateTime.UtcNow.AddDays(-40), BackupStatuses.Completed),
                History(BackupKinds.Scheduled, recent, directory, DateTime.UtcNow.AddDays(-1), BackupStatuses.Completed),
                History(BackupKinds.Manual, manual, directory, DateTime.UtcNow.AddDays(-40), BackupStatuses.Completed),
                History(BackupKinds.PreRestoreSafety, preRestore, directory, DateTime.UtcNow.AddDays(-40), BackupStatuses.Completed),
                History(BackupKinds.Scheduled, scheduled, "/somewhere-else", DateTime.UtcNow.AddDays(-40), BackupStatuses.Completed));
            await db.SaveChangesAsync();
        }

        await Service<IBackupService>().ApplyRetentionAsync(CancellationToken.None);
        Assert.False(File.Exists(Path.Combine(directory, scheduled)));
        Assert.True(File.Exists(Path.Combine(directory, recent)));
        Assert.True(File.Exists(Path.Combine(directory, manual)));
        Assert.True(File.Exists(Path.Combine(directory, preRestore)));
        Assert.True(File.Exists(Path.Combine(directory, stranger)));
        Assert.True(File.Exists(Path.Combine(directory, untracked)));
    }

    [SkippableFact]
    public async Task Restore_does_not_start_when_verification_fails()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        await SignInAsync();
        var junk = Path.Combine(BackupSqlSupport.Directory(), "corrupt.bak");
        await File.WriteAllTextAsync(junk, "not-a-database-backup");
        var before = await CountRestoresAsync();
        var exception = await Assert.ThrowsAsync<UnAccessException>(() => Service<IRestoreService>().RestoreAsync(new RestoreRequest
        {
            BackupFile = junk,
            ConfirmationText = BackupMessages.ConfirmationText
        }, CancellationToken.None));
        Assert.Equal(BackupMessages.VerifyFailed, exception.UserMessage);
        Assert.Equal(before, await CountRestoresAsync());
    }

    [SkippableFact]
    public async Task Restore_does_not_start_without_typed_confirmation()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        await SignInAsync();
        var before = await CountRestoresAsync();
        var exception = await Assert.ThrowsAsync<UnAccessException>(() => Service<IRestoreService>().RestoreAsync(new RestoreRequest
        {
            BackupFile = Path.Combine(BackupSqlSupport.Directory(), "missing.bak"),
            ConfirmationText = "EVET"
        }, CancellationToken.None));
        Assert.Equal(BackupMessages.ConfirmationRejected, exception.UserMessage);
        Assert.Equal(before, await CountRestoresAsync());
    }

    [SkippableFact]
    public async Task Safety_backup_failure_stops_restore()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        await SignInAsync();
        var directory = BackupSqlSupport.Directory();
        await PutAsync(SettingKeys.BackupDirectory, directory);
        var backup = await Service<IBackupService>().CreateManualAsync(CancellationToken.None);
        var marker = await MarkerAsync();
        await PutAsync(SettingKeys.BackupDirectory, BackupSqlSupport.UnwritableDirectory());
        var exception = await Assert.ThrowsAsync<UnAccessException>(() => Service<IRestoreService>().RestoreAsync(new RestoreRequest
        {
            BackupFile = backup.FullPath!,
            ConfirmationText = BackupMessages.ConfirmationText
        }, CancellationToken.None));
        Assert.Equal(BackupMessages.SafetyFailed, exception.UserMessage);
        await using var db = await Factory().CreateDbContextAsync();
        Assert.True(await db.Personnel.AnyAsync(item => item.PersonnelNumber == marker));
    }

    [SkippableFact]
    public async Task Restore_and_backup_are_blocked_while_maintenance_lock_is_held()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        await SignInAsync();
        await PutAsync(SettingKeys.BackupDirectory, BackupSqlSupport.Directory());
        var held = await Service<IMaintenanceGate>().TryEnterExclusiveAsync(CancellationToken.None);
        Assert.NotNull(held);
        try
        {
            var restore = await Assert.ThrowsAsync<UnAccessException>(() => ((RestoreService)Service<IRestoreService>()).ExecuteAsync(new RestoreRequest
            {
                BackupFile = Path.Combine(BackupSqlSupport.Directory(), "blocked.bak"),
                ConfirmationText = BackupMessages.ConfirmationText
            }, CancellationToken.None));
            Assert.Equal(BackupMessages.Busy, restore.UserMessage);
            var backup = await Assert.ThrowsAsync<UnAccessException>(() => Service<IBackupService>().CreateManualAsync(CancellationToken.None));
            Assert.Equal(BackupMessages.Busy, backup.UserMessage);
        }
        finally
        {
            await held.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task Stale_backup_history_is_recovered_without_a_stuck_lock()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        int staleId;
        int freshId;
        await using (var db = await Factory().CreateDbContextAsync())
        {
            var stale = History(BackupKinds.Manual, "stale.bak", "/tmp", DateTime.UtcNow.AddHours(-4), BackupStatuses.Running);
            stale.CompletedAtUtc = null;
            var fresh = History(BackupKinds.Manual, "fresh.bak", "/tmp", DateTime.UtcNow, BackupStatuses.Running);
            fresh.CompletedAtUtc = null;
            db.BackupHistories.AddRange(stale, fresh);
            await db.SaveChangesAsync();
            staleId = stale.Id;
            freshId = fresh.Id;
        }

        await Service<IBackupService>().RecoverStaleAsync(CancellationToken.None);
        await using var check = await Factory().CreateDbContextAsync();
        Assert.Equal(BackupStatuses.Failed, (await check.BackupHistories.SingleAsync(item => item.Id == staleId)).Status);
        Assert.Equal(BackupMessages.Abandoned, (await check.BackupHistories.SingleAsync(item => item.Id == staleId)).ErrorMessage);
        Assert.Equal(BackupStatuses.Running, (await check.BackupHistories.SingleAsync(item => item.Id == freshId)).Status);
    }

    [SkippableFact]
    public async Task Backup_setting_change_is_audited()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        await SignInAsync();
        await Service<ISystemSettingService>().SaveAsync(
        [
            new SystemSettingItem { Key = SettingKeys.BackupRetentionDays, Value = "45" }
        ], CancellationToken.None);
        await using var db = await Factory().CreateDbContextAsync();
        Assert.Contains(await db.AuditLogs.Select(item => item.Action).ToListAsync(), action => action == AuditActions.BackupSettingsChanged);
    }

    [SkippableFact]
    public async Task Database_health_confirms_sql_and_readiness_is_blocked_without_an_admin()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        await using var ready = await BackupSqlSupport.CreateAsync("Development", autoMigrate: true, seed: true, initialize: true);
        var health = await ready.Provider.GetRequiredService<IDatabaseHealthService>().GetAsync(CancellationToken.None);
        Assert.True(health.Connected);
        Assert.False(health.PendingMigrations);
        Assert.Equal("Güncel", health.MigrationText);
        Assert.True(health.TablesReadable);
        var report = await ready.Provider.GetRequiredService<IInstallationReadinessService>().EvaluateAsync(CancellationToken.None);
        Assert.Contains(report.Items, item => item.Name == "İlk yönetici" && item.Level == ReadinessLevels.Error);
        Assert.False(report.IsComplete);
    }

    [SkippableFact]
    public async Task Pending_migration_is_reported()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var name = "UnAccess_" + Guid.NewGuid().ToString("N");
        var connection = new SqlConnectionStringBuilder(_database.ConnectionString) { InitialCatalog = name }.ConnectionString;
        await BackupSqlSupport.ExecuteAsync(BackupSqlSupport.Master(_database.ConnectionString), "DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@name); EXEC sp_executesql @sql, N'@name sysname', @name", ("@name", name));
        try
        {
            await using var scope = await BackupSqlSupport.CreateAsync("Development", autoMigrate: false, seed: false, initialize: false, connection, name);
            var health = await scope.Provider.GetRequiredService<IDatabaseHealthService>().GetAsync(CancellationToken.None);
            Assert.True(health.Connected);
            Assert.True(health.PendingMigrations);
            Assert.Equal("Bekleyen migration var", health.MigrationText);
        }
        finally
        {
            await BackupSqlSupport.DropAsync(connection, name);
        }
    }

    [SkippableFact]
    public async Task Device_service_heartbeat_continues_while_maintenance_lock_is_held()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var gate = Service<IMaintenanceGate>();
        var held = await gate.TryEnterExclusiveAsync(CancellationToken.None);
        Assert.NotNull(held);
        try
        {
            var worker = new DeviceSyncWorker(
                Service<IDeviceSyncProcessor>(),
                new IdleClients(),
                Service<IUtcClock>(),
                Service<DeviceSyncOptions>(),
                NullLogger<DeviceSyncWorker>.Instance,
                Service<IRuntimeSettings>(),
                Service<IServiceMonitor>(),
                gate);
            await worker.ProcessBatchAsync(CancellationToken.None);
        }
        finally
        {
            await held.DisposeAsync();
        }

        await using var db = await Factory().CreateDbContextAsync();
        Assert.True(await db.ServiceHeartbeats.AnyAsync(item => item.ServiceName == ServiceNames.DeviceSync));
    }

    [SkippableFact]
    public async Task Production_does_not_migrate_or_seed()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var name = "UnAccess_" + Guid.NewGuid().ToString("N");
        var connection = new SqlConnectionStringBuilder(_database.ConnectionString) { InitialCatalog = name }.ConnectionString;
        await using var scope = await BackupSqlSupport.CreateAsync("Production", autoMigrate: true, seed: true, initialize: true, connection, name);
        Assert.Equal(0, await BackupSqlSupport.CountDatabasesAsync(_database.ConnectionString, name));
        await using var db = await Factory().CreateDbContextAsync();
        Assert.False(await db.Personnel.AnyAsync(item => item.PersonnelNumber == "TEST001"));
        Assert.False(await db.Cards.AnyAsync(item => item.CardNumber == "TESTCARD001"));
        Assert.False(await db.Users.AnyAsync(item => item.Username == "TEST001"));
    }

    [SkippableFact]
    public async Task Successful_safety_backup_allows_restore_and_writes_audit()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        await using var scope = await BackupSqlSupport.CreateAsync("Development", true, true, true);
        await BackupSqlSupport.SignInAsync(scope.Provider);
        var directory = BackupSqlSupport.Directory();
        await BackupSqlSupport.PutAsync(scope.Provider, SettingKeys.BackupDirectory, directory);
        var backup = await scope.Provider.GetRequiredService<IBackupService>().CreateManualAsync(CancellationToken.None);
        Assert.True(backup.Succeeded, backup.Message);
        var marker = await BackupSqlSupport.MarkerAsync(scope.Provider);
        var result = await scope.Provider.GetRequiredService<IRestoreService>().RestoreAsync(new RestoreRequest
        {
            BackupFile = backup.FullPath!,
            ConfirmationText = BackupMessages.ConfirmationText
        }, CancellationToken.None);
        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.SafetyBackupFileName);
        var factory = scope.Provider.GetRequiredService<IDbContextFactory<UnAccessDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        Assert.False(await db.Personnel.AnyAsync(item => item.PersonnelNumber == marker));
        Assert.True(await db.Departments.AnyAsync());
        Assert.Contains(await db.AuditLogs.Select(item => item.Action).ToListAsync(), action => action == AuditActions.RestoreCompleted);
        Assert.Contains(await db.RestoreHistories.Select(item => item.Status).ToListAsync(), status => status == BackupStatuses.Completed);
        Assert.NotEqual("UnAccessDB", scope.Name);
    }

    [SkippableFact]
    public async Task Failed_restore_records_history_and_log_without_dropping_the_database()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var logs = new CollectingLoggerProvider();
        await using var scope = await BackupSqlSupport.CreateAsync("Development", true, true, true, logs: logs);
        await BackupSqlSupport.SignInAsync(scope.Provider);
        var directory = BackupSqlSupport.Directory();
        await BackupSqlSupport.PutAsync(scope.Provider, SettingKeys.BackupDirectory, directory);
        var marker = await BackupSqlSupport.MarkerAsync(scope.Provider);
        var junk = Path.Combine(directory, "corrupt.bak");
        await File.WriteAllTextAsync(junk, "not-a-database-backup");
        var restore = (RestoreService)scope.Provider.GetRequiredService<IRestoreService>();
        var exception = await Assert.ThrowsAsync<UnAccessException>(() => restore.ExecuteAsync(new RestoreRequest
        {
            BackupFile = junk,
            ConfirmationText = BackupMessages.ConfirmationText
        }, CancellationToken.None));
        Assert.Equal(BackupMessages.RestoreFailed, exception.UserMessage);
        var factory = scope.Provider.GetRequiredService<IDbContextFactory<UnAccessDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        Assert.True(await db.Personnel.AnyAsync(item => item.PersonnelNumber == marker));
        Assert.Contains(await db.RestoreHistories.Select(item => item.Status).ToListAsync(), status => status == BackupStatuses.Failed);
        Assert.Contains(await db.AuditLogs.Select(item => item.Action).ToListAsync(), action => action == AuditActions.RestoreFailed);
        var written = string.Join(Environment.NewLine, logs.Lines);
        Assert.Contains("Geri yükleme sonucu", written, StringComparison.Ordinal);
        Assert.Contains(BackupStatuses.Failed, written, StringComparison.Ordinal);
        Assert.DoesNotContain(AdminPassword, written, StringComparison.Ordinal);
        Assert.Equal(1, await BackupSqlSupport.CountDatabasesAsync(_database.ConnectionString, scope.Name));
    }

    private async Task<BackupResult> ForceOneScheduledAsync()
    {
        await ClearScheduledAsync();
        return await Service<IBackupService>().CreateScheduledAsync(CancellationToken.None);
    }

    private async Task ClearScheduledAsync()
    {
        await using var db = await Factory().CreateDbContextAsync();
        var rows = await db.BackupHistories.Where(item => item.BackupType == BackupKinds.Scheduled && item.Status == BackupStatuses.Completed).ToListAsync();
        foreach (var row in rows)
        {
            row.Status = BackupStatuses.Failed;
            row.ErrorMessage = BackupMessages.Abandoned;
        }

        await db.SaveChangesAsync();
    }

    private async Task<int> CountScheduledAsync()
    {
        await using var db = await Factory().CreateDbContextAsync();
        return await db.BackupHistories.CountAsync(item => item.BackupType == BackupKinds.Scheduled && item.Status == BackupStatuses.Completed);
    }

    private async Task<int> CountRestoresAsync()
    {
        await using var db = await Factory().CreateDbContextAsync();
        return await db.RestoreHistories.CountAsync();
    }

    private async Task<string> MarkerAsync() => await BackupSqlSupport.MarkerAsync(_database.Provider!);

    private async Task SignInAsync() => await BackupSqlSupport.SignInAsync(_database.Provider!);

    private async Task PutAsync(string key, string value) => await BackupSqlSupport.PutAsync(_database.Provider!, key, value);

    private IDbContextFactory<UnAccessDbContext> Factory() => _database.Provider!.GetRequiredService<IDbContextFactory<UnAccessDbContext>>();

    private T Service<T>() where T : notnull => _database.Provider!.GetRequiredService<T>();

    private static BackupHistory History(string kind, string file, string directory, DateTime completed, string status) => new()
    {
        BackupType = kind,
        FileName = file,
        BackupDirectory = directory,
        StartedAtUtc = completed,
        CompletedAtUtc = status == BackupStatuses.Running ? null : completed,
        Status = status,
        TriggeredBy = kind == BackupKinds.Scheduled ? BackupTriggers.DeviceService : BackupTriggers.User
    };

    private sealed class IdleClients : IControllerClientFactory
    {
        public IAccessController Create(int controllerId) => throw new InvalidOperationException("Boş tur cihaz açmaz.");
    }
}

internal static class BackupSqlSupport
{
    public static string UnwritableDirectory()
    {
        var blocker = Path.Combine(Path.GetTempPath(), "unaccess-block-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(blocker, "block");
        return blocker + "/backups";
    }

    public static string Directory()
    {
        var path = Path.Combine(Path.GetTempPath(), "unaccess-bak-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
        }

        return path;
    }

    public static string Master(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
        return builder.ConnectionString;
    }

    public static async Task ExecuteAsync(string connectionString, string sql, params (string Name, string Value)[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters)
            command.Parameters.Add(new SqlParameter(parameter.Name, parameter.Value));
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<int> CountDatabasesAsync(string connectionString, string name)
    {
        await using var connection = new SqlConnection(Master(connectionString));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @name";
        command.Parameters.Add(new SqlParameter("@name", name));
        return (int)(await command.ExecuteScalarAsync() ?? 0);
    }

    public static async Task DropAsync(string connectionString, string name)
    {
        if (string.Equals(name, "UnAccessDB", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Üretim veritabanı silinemez.");
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(Master(connectionString));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF DB_ID(@name) IS NOT NULL
            BEGIN
                DECLARE @sql nvarchar(max) = N'ALTER DATABASE ' + QUOTENAME(@name) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE';
                EXEC sp_executesql @sql;
                SET @sql = N'DROP DATABASE ' + QUOTENAME(@name);
                EXEC sp_executesql @sql;
            END
            """;
        command.Parameters.Add(new SqlParameter("@name", name));
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<CatalogScope> CreateAsync(string environment, bool autoMigrate, bool seed, bool initialize, string? connectionString = null, string? name = null, CollectingLoggerProvider? logs = null)
    {
        var origin = Environment.GetEnvironmentVariable("ConnectionStrings__UnAccess");
        if (string.IsNullOrWhiteSpace(origin))
            throw new InvalidOperationException("ConnectionStrings__UnAccess tanımlı değil.");
        name ??= "UnAccess_" + Guid.NewGuid().ToString("N");
        if (string.Equals(name, "UnAccessDB", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Test veritabanı üretim adını kullanamaz.");
        connectionString ??= new SqlConnectionStringBuilder(origin) { InitialCatalog = name }.ConnectionString;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:UnAccess"] = connectionString,
                ["Database:AutoMigrate"] = autoMigrate ? "true" : "false",
                ["Seed:Enabled"] = seed ? "true" : "false",
                ["UnAccess:Environment"] = environment
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Information);
            if (logs != null)
                logging.AddProvider(logs);
        });
        services.AddUnAccessData(configuration);
        var provider = services.BuildServiceProvider();
        if (initialize)
            await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
        return new CatalogScope(provider, name, connectionString);
    }

    public static async Task SignInAsync(ServiceProvider provider)
    {
        var accounts = provider.GetRequiredService<IAccountService>();
        var current = provider.GetRequiredService<ICurrentUser>();
        if (!await accounts.HasAnyUserAsync(CancellationToken.None))
        {
            var created = await accounts.CreateInitialAdminAsync(new InitialAdminModel
            {
                Username = "phase5admin",
                FullName = "Phase Five Admin",
                Password = "Phase5-Admin-1"
            }, CancellationToken.None);
            if (!created.Succeeded)
                throw new InvalidOperationException(created.Message);
            return;
        }

        if (!current.IsAuthenticated)
        {
            var login = await accounts.LoginAsync("phase5admin", "Phase5-Admin-1", CancellationToken.None);
            if (!login.Succeeded)
                throw new InvalidOperationException(login.Message);
        }
    }

    public static async Task PutAsync(ServiceProvider provider, string key, string value)
    {
        var factory = provider.GetRequiredService<IDbContextFactory<UnAccessDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        var row = await db.SystemSettings.SingleAsync(item => item.Key == key);
        row.Value = value;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public static async Task<string> MarkerAsync(ServiceProvider provider)
    {
        var personnel = provider.GetRequiredService<IPersonnelService>();
        var departments = await personnel.GetDepartmentsAsync(CancellationToken.None);
        var jobs = await personnel.GetJobTitlesAsync(CancellationToken.None);
        var number = "M" + Guid.NewGuid().ToString("N")[..8];
        await personnel.CreateAsync(new PersonnelEditModel
        {
            PersonnelNumber = number,
            FirstName = "Yedek",
            LastName = "Isaret",
            DepartmentId = departments.Single(item => item.Code == "GC").Id,
            JobTitleId = jobs.Single(item => item.Code == "DOKTOR").Id,
            IsActive = true
        }, CancellationToken.None);
        return number;
    }
}

internal sealed class CatalogScope : IAsyncDisposable
{
    public CatalogScope(ServiceProvider provider, string name, string connectionString)
    {
        Provider = provider;
        Name = name;
        ConnectionString = connectionString;
    }

    public ServiceProvider Provider { get; }
    public string Name { get; }
    public string ConnectionString { get; }

    public async ValueTask DisposeAsync()
    {
        await Provider.DisposeAsync();
        await BackupSqlSupport.DropAsync(ConnectionString, Name);
    }
}

internal sealed class CollectingLoggerProvider : ILoggerProvider
{
    public List<string> Lines { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Collector(Lines);

    public void Dispose()
    {
    }

    private sealed class Collector : ILogger
    {
        private readonly List<string> _lines;

        public Collector(List<string> lines) => _lines = lines;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var line = formatter(state, exception);
            if (exception != null)
                line += Environment.NewLine + exception;
            lock (_lines)
                _lines.Add(line);
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }
}
