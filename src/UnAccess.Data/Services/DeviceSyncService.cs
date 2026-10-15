using Microsoft.EntityFrameworkCore;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Audit;
using UnAccess.Core.Enums;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Core.Sync;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Data.Services;

internal sealed class DeviceSyncService : ServiceBase, IDeviceSyncService
{
    private readonly IDeviceSyncOutbox _outbox;
    private readonly IUtcClock _clock;
    private readonly IHospitalClock _hospitalClock;

    public DeviceSyncService(
        IDbContextFactory<UnAccessDbContext> factory,
        AuditWriter audit,
        Microsoft.Extensions.Logging.ILogger<DeviceSyncService> logger,
        IDeviceSyncOutbox outbox,
        IUtcClock clock,
        IHospitalClock hospitalClock)
        : base(factory, audit, logger)
    {
        _outbox = outbox;
        _clock = clock;
        _hospitalClock = hospitalClock;
    }

    public Task<DeviceSyncSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var counts = await db.DeviceSyncQueue.AsNoTracking()
                .GroupBy(item => item.Status)
                .Select(group => new { Status = group.Key, Count = group.Count() })
                .ToListAsync(ct);
            int Count(DeviceSyncStatus status) => counts.FirstOrDefault(item => item.Status == status)?.Count ?? 0;
            return new DeviceSyncSummary
            {
                Pending = Count(DeviceSyncStatus.Pending),
                Processing = Count(DeviceSyncStatus.Processing),
                Failed = Count(DeviceSyncStatus.Failed) + Count(DeviceSyncStatus.DeadLetter),
                Completed = Count(DeviceSyncStatus.Completed)
            };
        }, cancellationToken);
    }

    public Task<IReadOnlyList<DeviceSyncJobItem>> SearchAsync(DeviceSyncFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return QueryAsync(async (db, ct) =>
        {
            var query = db.DeviceSyncQueue.AsNoTracking();
            if (filter.ControllerId is int controllerId && controllerId > 0)
                query = query.Where(item => item.ControllerId == controllerId);
            if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<DeviceSyncStatus>(filter.Status, out var status))
                query = query.Where(item => item.Status == status);
            if (!string.IsNullOrWhiteSpace(filter.OperationType))
                query = query.Where(item => item.OperationType == filter.OperationType);

            var rows = await (
                from job in query
                join controller in db.Controllers.AsNoTracking() on job.ControllerId equals controller.Id
                orderby job.CreatedAtUtc descending
                select new { job, controller.Name })
                .Take(200)
                .ToListAsync(ct);

            return (IReadOnlyList<DeviceSyncJobItem>)rows.Select(row => new DeviceSyncJobItem
            {
                Id = row.job.Id,
                CreatedAtText = Format(row.job.CreatedAtUtc),
                ControllerName = row.Name,
                OperationText = OperationText(row.job.OperationType),
                EntityText = EntityText(row.job.EntityType, row.job.EntityId, row.job.Payload),
                Status = row.job.Status.ToString(),
                StatusText = StatusText(row.job.Status),
                AttemptCount = row.job.AttemptCount,
                LastAttemptText = row.job.StartedAtUtc is DateTime started ? Format(started) : "",
                LastError = row.job.LastError
            }).ToList();
        }, cancellationToken);
    }

    public Task<int> RetryFailedAsync(CancellationToken cancellationToken)
    {
        var count = 0;
        return SaveWithAuditAsync(async (db, ct) =>
        {
            var jobs = await db.DeviceSyncQueue
                .Where(item => item.Status == DeviceSyncStatus.Failed || item.Status == DeviceSyncStatus.DeadLetter)
                .ToListAsync(ct);
            var now = _clock.UtcNow;
            foreach (var job in jobs)
            {
                job.Status = DeviceSyncStatus.Pending;
                job.AttemptCount = 0;
                job.NextAttemptAtUtc = now;
                job.StartedAtUtc = null;
                job.CompletedAtUtc = null;
                job.LastError = null;
            }

            count = jobs.Count;
            return count;
        }, saved => new AuditEntry
        {
            Action = AuditActions.RetryRequested,
            EntityType = AuditEntities.DeviceSync,
            EntityId = "failed",
            Description = $"Başarısız senkronizasyon işleri yeniden denemeye alındı: {saved}"
        }, cancellationToken);
    }

    public Task RequestFullSyncAsync(int controllerId, CancellationToken cancellationToken)
    {
        string code = "";
        return SaveWithAuditAsync(async (db, ct) =>
        {
            var controller = await db.Controllers.FirstOrDefaultAsync(item => item.Id == controllerId, ct)
                ?? throw new UnAccessException("Kontrolör bulunamadı.");
            if (!controller.IsActive)
                throw new UnAccessException("Pasif kontrolör senkronize edilemez.");
            code = controller.Code;
            await _outbox.EnqueueFullSyncAsync(db, controller.Id, ct);
            return controller.Id;
        }, saved => new AuditEntry
        {
            Action = AuditActions.FullSyncRequested,
            EntityType = AuditEntities.DeviceSync,
            EntityId = saved.ToString(),
            Description = $"Kontrolör tam senkronizasyonu kuyruğa alındı: {code}"
        }, cancellationToken);
    }

    private string Format(DateTime utc)
    {
        var value = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return _hospitalClock.ToHospitalLocal(value).ToString("dd.MM.yyyy HH:mm:ss");
    }

    private static string OperationText(string operation) => operation switch
    {
        DeviceSyncOperations.UpsertCard => "Kart yaz",
        DeviceSyncOperations.RemoveCard => "Kart sil",
        DeviceSyncOperations.SyncCardPermissions => "Yetki senkronu",
        DeviceSyncOperations.FullControllerSync => "Tam senkron",
        _ => operation
    };

    private static string StatusText(DeviceSyncStatus status) => status switch
    {
        DeviceSyncStatus.Pending => "Bekliyor",
        DeviceSyncStatus.Processing => "İşleniyor",
        DeviceSyncStatus.Completed => "Tamamlandı",
        DeviceSyncStatus.Failed => "Başarısız",
        DeviceSyncStatus.DeadLetter => "Teslim edilemedi",
        _ => status.ToString()
    };

    private static string EntityText(string entityType, string entityId, string payload)
    {
        var cardNumber = SyncPayload.CardNumber(payload);
        return entityType switch
        {
            DeviceSyncEntities.Card when !string.IsNullOrWhiteSpace(cardNumber) => "Kart " + cardNumber,
            DeviceSyncEntities.Personnel => "Personel " + entityId,
            DeviceSyncEntities.Controller => "Kontrolör " + entityId,
            _ => entityType + " " + entityId
        };
    }
}
