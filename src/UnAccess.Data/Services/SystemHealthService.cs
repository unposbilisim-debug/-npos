using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Backup;
using UnAccess.Core.Enums;
using UnAccess.Core.Models;
using UnAccess.Core.Security;
using UnAccess.Core.Sync;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Data.Services;

internal sealed class SystemHealthService : ISystemHealthService
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly IUtcClock _clock;
    private readonly IAccessGuard _guard;
    private readonly IRuntimeSettings _settings;
    private readonly IDatabaseHealthService _databaseHealth;
    private readonly string _dataSource;

    public SystemHealthService(
        IDbContextFactory<UnAccessDbContext> factory,
        IUtcClock clock,
        IAccessGuard guard,
        IRuntimeSettings settings,
        IDatabaseHealthService databaseHealth,
        IConfiguration configuration)
    {
        _factory = factory;
        _clock = clock;
        _guard = guard;
        _settings = settings;
        _databaseHealth = databaseHealth;
        var connection = configuration.GetConnectionString("UnAccess");
        _dataSource = string.IsNullOrWhiteSpace(connection) ? "" : SqlIdentifiers.DataSource(connection);
    }

    public async Task<SystemHealthReport> GetAsync(CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.SystemView);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var connected = false;
        try
        {
            connected = await db.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception)
        {
            connected = false;
        }

        if (!connected)
        {
            return new SystemHealthReport
            {
                DatabaseConnected = false,
                DatabaseText = "Hata",
                DeviceServiceText = HeartbeatStatus.NotResponding
            };
        }

        var now = _clock.UtcNow;
        var stale = await _settings.HeartbeatStaleAsync(cancellationToken);
        var lastSeen = await db.ServiceHeartbeats.AsNoTracking()
            .Where(item => item.ServiceName == ServiceNames.DeviceSync)
            .OrderByDescending(item => item.LastSeenAtUtc)
            .Select(item => (DateTime?)item.LastSeenAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var alive = lastSeen is DateTime seen && HeartbeatStatus.IsAlive(seen, now, stale);

        var controllers = await db.Controllers.AsNoTracking()
            .Where(item => item.IsActive)
            .GroupBy(item => item.ConnectionStatus)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        int Count(params string[] statuses) => controllers.Where(item => statuses.Contains(item.Status)).Sum(item => item.Count);

        var sync = await db.DeviceSyncQueue.AsNoTracking()
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        int Sync(DeviceSyncStatus status) => sync.FirstOrDefault(item => item.Status == status)?.Count ?? 0;

        var lastEvent = await db.AccessEvents.AsNoTracking()
            .OrderByDescending(item => item.OccurredAtUtc)
            .Select(item => (DateTime?)item.OccurredAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var database = await _databaseHealth.GetAsync(cancellationToken);
        var backup = await BackupSnapshotAsync(db, cancellationToken);
        return new SystemHealthReport
        {
            DatabaseConnected = true,
            DatabaseText = "Bağlı",
            DeviceServiceText = alive ? HeartbeatStatus.Running : HeartbeatStatus.NotResponding,
            DeviceServiceLastSeenUtc = lastSeen,
            ControllersOnline = Count(ControllerConnectionStatuses.Online),
            ControllersOffline = Count(ControllerConnectionStatuses.Offline, ControllerConnectionStatuses.Error),
            ControllersUnknown = Count(ControllerConnectionStatuses.Unknown, ""),
            SyncPending = Sync(DeviceSyncStatus.Pending),
            SyncFailed = Sync(DeviceSyncStatus.Failed),
            SyncDeadLetter = Sync(DeviceSyncStatus.DeadLetter),
            LastAccessEventUtc = lastEvent,
            MigrationText = database.MigrationText,
            ModelText = database.ModelText,
            BackupStatusText = backup.Status,
            LastSuccessfulBackupUtc = backup.LastSuccess,
            LastFailedBackupUtc = backup.LastFailure,
            BackupDirectory = backup.Directory,
            BackupFolderText = BackupDisk.FolderAccess(_dataSource, backup.Directory),
            DiskSpaceText = BackupDisk.FreeSpace(_dataSource, backup.Directory),
            BackupEnabled = backup.Enabled,
            BackupTimeText = backup.Time,
            BackupRetentionDays = backup.Retention
        };
    }

    private async Task<(string Status, DateTime? LastSuccess, DateTime? LastFailure, string Directory, bool Enabled, string Time, int Retention)> BackupSnapshotAsync(UnAccessDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            var enabled = await _settings.BackupEnabledAsync(cancellationToken);
            var directory = await _settings.BackupDirectoryAsync(cancellationToken);
            var time = await _settings.BackupTimeAsync(cancellationToken);
            var retention = await _settings.BackupRetentionDaysAsync(cancellationToken);
            var lastSuccess = await db.BackupHistories.AsNoTracking()
                .Where(item => item.Status == BackupStatuses.Completed)
                .OrderByDescending(item => item.CompletedAtUtc)
                .Select(item => item.CompletedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            var lastFailure = await db.BackupHistories.AsNoTracking()
                .Where(item => item.Status == BackupStatuses.Failed)
                .OrderByDescending(item => item.CompletedAtUtc)
                .Select(item => item.CompletedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            var status = !enabled ? "Kapalı" : string.IsNullOrWhiteSpace(directory) ? "Klasör tanımlı değil" : "Açık";
            return (status, lastSuccess, lastFailure, directory, enabled, time.ToString("HH:mm"), retention);
        }
        catch (Exception)
        {
            return ("Bilinmiyor", null, null, "", false, "", 0);
        }
    }
}
