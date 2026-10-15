using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Enums;
using UnAccess.Core.Models;
using UnAccess.Core.Sync;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Data.Services;

internal sealed class ControllerQueryService : ServiceBase, IControllerQueryService
{
    public ControllerQueryService(
        IDbContextFactory<UnAccessDbContext> factory,
        AuditWriter audit,
        ILogger<ControllerQueryService> logger)
        : base(factory, audit, logger)
    {
    }

    public Task<IReadOnlyList<ControllerListItem>> ListAsync(CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var rows = await db.Controllers.AsNoTracking()
                .OrderBy(controller => controller.Name)
                .Select(controller => new ControllerListItem
                {
                    Id = controller.Id,
                    Name = controller.Name,
                    Model = controller.Model,
                    IpAddress = controller.IpAddress,
                    Port = controller.Port,
                    SerialNumber = controller.SerialNumber,
                    IsActive = controller.IsActive,
                    LastSeenAtUtc = controller.LastSeenAt,
                    ConnectionText = controller.ConnectionStatus == ControllerConnectionStatuses.Online
                        ? "Çevrimiçi"
                        : controller.ConnectionStatus == ControllerConnectionStatuses.Offline || controller.ConnectionStatus == ControllerConnectionStatuses.Error
                            ? "Çevrimdışı"
                            : "Bilinmiyor",
                    LastSuccessfulSyncAtUtc = controller.LastSuccessfulSyncAtUtc,
                    LastError = controller.LastError
                })
                .ToListAsync(ct);
            var pending = await db.DeviceSyncQueue.AsNoTracking()
                .Where(item => item.Status == DeviceSyncStatus.Pending)
                .GroupBy(item => item.ControllerId)
                .Select(group => new { ControllerId = group.Key, Count = group.Count() })
                .ToListAsync(ct);
            foreach (var row in rows)
                row.PendingJobs = pending.FirstOrDefault(item => item.ControllerId == row.Id)?.Count ?? 0;
            return (IReadOnlyList<ControllerListItem>)rows;
        }, cancellationToken);
    }
}
