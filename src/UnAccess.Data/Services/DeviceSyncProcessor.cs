using Microsoft.EntityFrameworkCore;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Enums;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Core.Sync;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Data.Services;

internal sealed class DeviceSyncProcessor : IDeviceSyncProcessor
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly IDevicePermissionProjectionService _projection;
    private readonly DeviceSyncOptions _options;

    public DeviceSyncProcessor(
        IDbContextFactory<UnAccessDbContext> factory,
        IDevicePermissionProjectionService projection,
        DeviceSyncOptions options)
    {
        _factory = factory;
        _projection = projection;
        _options = options;
    }

    public async Task RecoverStaleAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var cutoff = utcNow - _options.StaleProcessing;
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var ids = await db.DeviceSyncQueue
            .Where(item => item.Status == DeviceSyncStatus.Processing && item.StartedAtUtc != null && item.StartedAtUtc < cutoff)
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);
        if (ids.Count == 0)
            return;

        await db.DeviceSyncQueue
            .Where(item => ids.Contains(item.Id) && item.Status == DeviceSyncStatus.Processing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, DeviceSyncStatus.Pending)
                .SetProperty(item => item.StartedAtUtc, (DateTime?)null)
                .SetProperty(item => item.NextAttemptAtUtc, utcNow), cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceSyncJob>> ClaimAsync(int batchSize, DateTime utcNow, CancellationToken cancellationToken)
    {
        var size = batchSize > 0 ? batchSize : _options.BatchSize;
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var ids = await db.DeviceSyncQueue
            .Where(item => (item.Status == DeviceSyncStatus.Pending || item.Status == DeviceSyncStatus.Failed)
                && item.NextAttemptAtUtc <= utcNow)
            .OrderBy(item => item.Priority)
            .ThenBy(item => item.CreatedAtUtc)
            .Select(item => item.Id)
            .Take(size)
            .ToListAsync(cancellationToken);

        var claimed = new List<DeviceSyncJob>(ids.Count);
        foreach (var id in ids)
        {
            var updated = await db.DeviceSyncQueue
                .Where(item => item.Id == id
                    && (item.Status == DeviceSyncStatus.Pending || item.Status == DeviceSyncStatus.Failed)
                    && item.NextAttemptAtUtc <= utcNow)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, DeviceSyncStatus.Processing)
                    .SetProperty(item => item.StartedAtUtc, utcNow), cancellationToken);
            if (updated == 0)
                continue;

            var row = await db.DeviceSyncQueue.AsNoTracking().SingleAsync(item => item.Id == id, cancellationToken);
            claimed.Add(Map(row));
        }

        return claimed;
    }

    public async Task<DeviceCommand> BuildCommandAsync(DeviceSyncJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var controller = await db.Controllers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == job.ControllerId, cancellationToken);
        if (controller == null || !controller.IsActive)
        {
            return new DeviceCommand
            {
                Skip = true,
                SkipReason = "Kontrolör pasif veya bulunamadı.",
                ControllerId = job.ControllerId
            };
        }

        var command = job.OperationType switch
        {
            DeviceSyncOperations.UpsertCard => await BuildUpsertAsync(db, job, cancellationToken),
            DeviceSyncOperations.RemoveCard => BuildRemove(job),
            DeviceSyncOperations.SyncCardPermissions => await BuildSyncAsync(job, cancellationToken),
            DeviceSyncOperations.FullControllerSync => await BuildFullAsync(job, cancellationToken),
            _ => throw new UnAccessException("Bilinmeyen senkronizasyon işlemi.")
        };
        command.ControllerId = controller.Id;
        command.Host = controller.IpAddress;
        command.Port = controller.Port;
        command.ControllerCode = controller.Code;
        return command;
    }

    public Task CompleteAsync(long jobId, int controllerId, DateTime utcNow, CancellationToken cancellationToken) =>
        FinishAsync(jobId, controllerId, utcNow, success: true, error: null, cancellationToken);

    public Task FailAsync(long jobId, int controllerId, string error, DateTime utcNow, CancellationToken cancellationToken) =>
        FinishAsync(jobId, controllerId, utcNow, success: false, error: error, cancellationToken);

    private async Task FinishAsync(
        long jobId,
        int controllerId,
        DateTime utcNow,
        bool success,
        string? error,
        CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var job = await db.DeviceSyncQueue.FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken);
        if (job == null || job.Status != DeviceSyncStatus.Processing)
            return;

        if (success)
        {
            job.Status = DeviceSyncStatus.Completed;
            job.CompletedAtUtc = utcNow;
            job.LastError = null;
        }
        else
        {
            job.AttemptCount += 1;
            job.LastError = Trim(error);
            job.StartedAtUtc = utcNow;
            if (job.AttemptCount >= job.MaxAttempts)
            {
                job.Status = DeviceSyncStatus.DeadLetter;
                job.CompletedAtUtc = utcNow;
            }
            else
            {
                job.Status = DeviceSyncStatus.Failed;
                job.NextAttemptAtUtc = utcNow.Add(_options.DelayForAttempt(job.AttemptCount));
            }
        }

        var controller = await db.Controllers.FirstOrDefaultAsync(item => item.Id == controllerId, cancellationToken);
        if (controller != null)
        {
            controller.LastSyncAttemptAtUtc = utcNow;
            if (success)
            {
                controller.ConnectionStatus = ControllerConnectionStatuses.Online;
                controller.LastSuccessfulSyncAtUtc = utcNow;
                controller.LastError = null;
            }
            else
            {
                controller.ConnectionStatus = error != null && error.Contains("çevrimdışı", StringComparison.OrdinalIgnoreCase)
                    ? ControllerConnectionStatuses.Offline
                    : ControllerConnectionStatuses.Error;
                controller.LastError = Trim(error);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<DeviceCommand> BuildUpsertAsync(UnAccessDbContext db, DeviceSyncJob job, CancellationToken cancellationToken)
    {
        var body = SyncPayload.ReadCard(job.Payload);
        var card = await db.Cards.AsNoTracking().FirstOrDefaultAsync(item => item.Id == body.CardId, cancellationToken);
        if (card == null || card.Status != CardStatus.Active)
        {
            return new DeviceCommand
            {
                Kind = DeviceCommandKind.Remove,
                Card = new DeviceCardCommand { CardNumber = card?.CardNumber ?? body.CardNumber }
            };
        }

        var projection = await _projection.ProjectPersonnelAsync(card.PersonnelId, cancellationToken);
        var channels = projection.Controllers
            .Where(item => item.ControllerId == job.ControllerId)
            .SelectMany(item => item.Channels)
            .Select(ToChannel)
            .ToList();
        return new DeviceCommand
        {
            Kind = DeviceCommandKind.Upsert,
            Card = new DeviceCardCommand
            {
                CardNumber = card.CardNumber,
                PersonnelNumber = projection.PersonnelNumber,
                ValidFrom = card.ValidFrom,
                ValidUntil = card.ValidUntil,
                Channels = channels
            }
        };
    }

    private static DeviceCommand BuildRemove(DeviceSyncJob job)
    {
        var number = SyncPayload.CardNumber(job.Payload);
        if (string.IsNullOrWhiteSpace(number))
            return new DeviceCommand { Skip = true, SkipReason = "Kart numarası yok." };

        return new DeviceCommand
        {
            Kind = DeviceCommandKind.Remove,
            Card = new DeviceCardCommand { CardNumber = number }
        };
    }

    private async Task<DeviceCommand> BuildSyncAsync(DeviceSyncJob job, CancellationToken cancellationToken)
    {
        var personnelId = SyncPayload.ReadPersonnel(job.Payload).PersonnelId;
        if (personnelId <= 0 && int.TryParse(job.EntityId, out var parsed))
            personnelId = parsed;
        var projection = await _projection.ProjectPersonnelAsync(personnelId, cancellationToken);
        if (string.IsNullOrWhiteSpace(projection.CardNumber))
            return new DeviceCommand { Skip = true, SkipReason = "Aktif kart yok." };

        var channels = projection.Controllers
            .Where(item => item.ControllerId == job.ControllerId)
            .SelectMany(item => item.Channels)
            .Select(ToChannel)
            .ToList();
        if (channels.Count == 0)
        {
            return new DeviceCommand
            {
                Kind = DeviceCommandKind.Remove,
                Card = new DeviceCardCommand { CardNumber = projection.CardNumber, PersonnelNumber = projection.PersonnelNumber }
            };
        }

        return new DeviceCommand
        {
            Kind = DeviceCommandKind.Upsert,
            Card = new DeviceCardCommand
            {
                CardNumber = projection.CardNumber,
                PersonnelNumber = projection.PersonnelNumber,
                ValidFrom = projection.ValidFrom,
                ValidUntil = projection.ValidUntil,
                Channels = channels
            }
        };
    }

    private async Task<DeviceCommand> BuildFullAsync(DeviceSyncJob job, CancellationToken cancellationToken)
    {
        var cards = await _projection.ProjectControllerAsync(job.ControllerId, cancellationToken);
        return new DeviceCommand
        {
            Kind = DeviceCommandKind.Replace,
            Cards = cards
        };
    }

    private static DeviceChannelCommand ToChannel(DeviceChannelProjection channel) =>
        new()
        {
            ChannelNumber = channel.ChannelNumber,
            TimeScheduleId = channel.TimeScheduleId,
            TimeScheduleCode = channel.TimeScheduleCode,
            Windows = channel.Windows
        };

    private static DeviceSyncJob Map(UnAccess.Core.Entities.DeviceSyncQueue row) =>
        new()
        {
            Id = row.Id,
            OperationId = row.OperationId,
            ControllerId = row.ControllerId,
            OperationType = row.OperationType,
            EntityType = row.EntityType,
            EntityId = row.EntityId,
            Payload = row.Payload,
            AttemptCount = row.AttemptCount,
            MaxAttempts = row.MaxAttempts
        };

    private static string? Trim(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return null;
        var text = error.Trim();
        return text.Length <= 1000 ? text : text[..1000];
    }
}
