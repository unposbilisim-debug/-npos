using Microsoft.EntityFrameworkCore;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Entities;
using UnAccess.Core.Enums;
using UnAccess.Core.Sync;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Data.Services;

internal interface IDeviceSyncOutbox
{
    Task EnqueueCardAsync(UnAccessDbContext db, Card card, bool remove, CancellationToken cancellationToken);
    Task EnqueuePersonnelPermissionsAsync(UnAccessDbContext db, int personnelId, CancellationToken cancellationToken);
    Task EnqueueGroupPermissionsAsync(UnAccessDbContext db, int accessGroupId, CancellationToken cancellationToken);
    Task EnqueueSchedulePermissionsAsync(UnAccessDbContext db, int timeScheduleId, CancellationToken cancellationToken);
    Task EnqueueFullSyncAsync(UnAccessDbContext db, int controllerId, CancellationToken cancellationToken);
}

internal sealed class DeviceSyncOutbox : IDeviceSyncOutbox
{
    private readonly IUtcClock _clock;
    private readonly DeviceSyncOptions _options;

    public DeviceSyncOutbox(IUtcClock clock, DeviceSyncOptions options)
    {
        _clock = clock;
        _options = options;
    }

    public async Task EnqueueCardAsync(UnAccessDbContext db, Card card, bool remove, CancellationToken cancellationToken)
    {
        var operation = remove ? DeviceSyncOperations.RemoveCard : DeviceSyncOperations.UpsertCard;
        var priority = remove ? DeviceSyncPriority.Critical : DeviceSyncPriority.Normal;
        var payload = SyncPayload.Card(card.Id, card.CardNumber, card.PersonnelId);
        foreach (var controllerId in await ActiveControllersAsync(db, cancellationToken))
        {
            await AddAsync(
                db,
                controllerId,
                operation,
                DeviceSyncEntities.Card,
                card.Id.ToString(),
                payload,
                priority,
                cancellationToken);
        }
    }

    public async Task EnqueuePersonnelPermissionsAsync(UnAccessDbContext db, int personnelId, CancellationToken cancellationToken)
    {
        var payload = SyncPayload.Personnel(personnelId);
        foreach (var controllerId in await ActiveControllersAsync(db, cancellationToken))
        {
            await AddAsync(
                db,
                controllerId,
                DeviceSyncOperations.SyncCardPermissions,
                DeviceSyncEntities.Personnel,
                personnelId.ToString(),
                payload,
                DeviceSyncPriority.Normal,
                cancellationToken);
        }
    }

    public async Task EnqueueGroupPermissionsAsync(UnAccessDbContext db, int accessGroupId, CancellationToken cancellationToken)
    {
        var personnelIds = await (
            from membership in db.PersonnelAccessGroups
            join person in db.Personnel on membership.PersonnelId equals person.Id
            where membership.AccessGroupId == accessGroupId
                && membership.IsActive
                && person.IsActive
                && db.Cards.Any(card => card.PersonnelId == person.Id && card.Status == CardStatus.Active)
            select person.Id).Distinct().ToListAsync(cancellationToken);

        foreach (var personnelId in personnelIds)
            await EnqueuePersonnelPermissionsAsync(db, personnelId, cancellationToken);
    }

    public async Task EnqueueSchedulePermissionsAsync(UnAccessDbContext db, int timeScheduleId, CancellationToken cancellationToken)
    {
        var fromGroups = await (
            from membership in db.PersonnelAccessGroups
            join person in db.Personnel on membership.PersonnelId equals person.Id
            where membership.IsActive && person.IsActive
                && db.Cards.Any(card => card.PersonnelId == person.Id && card.Status == CardStatus.Active)
                && (db.AccessGroupZones.Any(rule => rule.IsActive && rule.AccessGroupId == membership.AccessGroupId && rule.TimeScheduleId == timeScheduleId)
                    || db.AccessGroupDoors.Any(rule => rule.IsActive && rule.AccessGroupId == membership.AccessGroupId && rule.TimeScheduleId == timeScheduleId))
            select person.Id).Distinct().ToListAsync(cancellationToken);

        var fromOverrides = await (
            from rule in db.PersonnelAccessOverrides
            join person in db.Personnel on rule.PersonnelId equals person.Id
            where rule.IsActive && rule.TimeScheduleId == timeScheduleId && person.IsActive
                && db.Cards.Any(card => card.PersonnelId == person.Id && card.Status == CardStatus.Active)
            select person.Id).Distinct().ToListAsync(cancellationToken);

        foreach (var personnelId in fromGroups.Concat(fromOverrides).Distinct())
            await EnqueuePersonnelPermissionsAsync(db, personnelId, cancellationToken);
    }

    public Task EnqueueFullSyncAsync(UnAccessDbContext db, int controllerId, CancellationToken cancellationToken)
    {
        return AddAsync(
            db,
            controllerId,
            DeviceSyncOperations.FullControllerSync,
            DeviceSyncEntities.Controller,
            controllerId.ToString(),
            SyncPayload.Controller(controllerId),
            DeviceSyncPriority.Low,
            cancellationToken);
    }

    private async Task AddAsync(
        UnAccessDbContext db,
        int controllerId,
        string operation,
        string entityType,
        string entityId,
        string payload,
        int priority,
        CancellationToken cancellationToken)
    {
        var existing = await db.DeviceSyncQueue.FirstOrDefaultAsync(
            item => item.ControllerId == controllerId
                && item.OperationType == operation
                && item.EntityType == entityType
                && item.EntityId == entityId
                && (item.Status == DeviceSyncStatus.Pending
                    || item.Status == DeviceSyncStatus.Failed
                    || item.Status == DeviceSyncStatus.DeadLetter),
            cancellationToken);

        var now = _clock.UtcNow;
        if (existing != null)
        {
            if (existing.Status == DeviceSyncStatus.Pending)
                return;

            existing.Status = DeviceSyncStatus.Pending;
            existing.AttemptCount = 0;
            existing.NextAttemptAtUtc = now;
            existing.StartedAtUtc = null;
            existing.CompletedAtUtc = null;
            existing.LastError = null;
            existing.Payload = payload;
            existing.Priority = priority;
            return;
        }

        db.DeviceSyncQueue.Add(new DeviceSyncQueue
        {
            OperationId = Guid.NewGuid().ToString("N"),
            ControllerId = controllerId,
            OperationType = operation,
            EntityType = entityType,
            EntityId = entityId,
            Payload = payload,
            Status = DeviceSyncStatus.Pending,
            Priority = priority,
            AttemptCount = 0,
            MaxAttempts = _options.MaxAttempts,
            NextAttemptAtUtc = now,
            CreatedAtUtc = now
        });
    }

    private static Task<List<int>> ActiveControllersAsync(UnAccessDbContext db, CancellationToken cancellationToken) =>
        db.Controllers.Where(controller => controller.IsActive).Select(controller => controller.Id).ToListAsync(cancellationToken);
}
