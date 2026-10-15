using Microsoft.EntityFrameworkCore;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Enums;
using UnAccess.Core.Models;
using UnAccess.Core.Security;
using UnAccess.Core.Sync;

namespace UnAccess.Data.Services;

internal sealed class SystemHealthService : ISystemHealthService
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly IUtcClock _clock;
    private readonly IAccessGuard _guard;
    private readonly IRuntimeSettings _settings;

    public SystemHealthService(
        IDbContextFactory<UnAccessDbContext> factory,
        IUtcClock clock,
        IAccessGuard guard,
        IRuntimeSettings settings)
    {
        _factory = factory;
        _clock = clock;
        _guard = guard;
        _settings = settings;
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
            LastAccessEventUtc = lastEvent
        };
    }
}
