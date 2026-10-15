using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Audit;
using UnAccess.Core.Common;
using UnAccess.Core.Entities;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Data.Services;

internal sealed class PersonnelAuthorizationService : ServiceBase, IPersonnelAuthorizationService
{
    private readonly IAccessDecisionService _decisions;

    public PersonnelAuthorizationService(
        IDbContextFactory<UnAccessDbContext> factory,
        AuditWriter audit,
        ILogger<PersonnelAuthorizationService> logger,
        IAccessDecisionService decisions)
        : base(factory, audit, logger)
    {
        _decisions = decisions;
    }

    public async Task<PersonnelAuthorizationView> GetAsync(int personnelId, CancellationToken cancellationToken)
    {
        var groups = await QueryAsync(async (db, ct) =>
        {
            var rows = await (
                from membership in db.PersonnelAccessGroups.AsNoTracking()
                join groupItem in db.AccessGroups.AsNoTracking() on membership.AccessGroupId equals groupItem.Id
                where membership.PersonnelId == personnelId
                orderby groupItem.Name
                select new PersonnelGroupItem
                {
                    Id = membership.Id,
                    AccessGroupId = groupItem.Id,
                    Code = groupItem.Code,
                    Name = groupItem.Name,
                    ValidFrom = membership.ValidFrom,
                    ValidUntil = membership.ValidUntil,
                    IsActive = membership.IsActive && groupItem.IsActive
                }).ToListAsync(ct);
            return (IReadOnlyList<PersonnelGroupItem>)rows;
        }, cancellationToken);

        var overrides = await QueryAsync(async (db, ct) =>
        {
            var rows = await db.PersonnelAccessOverrides.AsNoTracking()
                .Where(item => item.PersonnelId == personnelId)
                .Select(item => new PersonnelOverrideItem
                {
                    Id = item.Id,
                    ZoneId = item.ZoneId,
                    DoorId = item.DoorId,
                    TargetName = item.DoorId != null
                        ? item.Door!.Name
                        : item.Zone!.Name,
                    TimeScheduleId = item.TimeScheduleId,
                    ScheduleName = item.TimeSchedule.Name,
                    IsAllowed = item.IsAllowed,
                    ValidFrom = item.ValidFrom,
                    ValidUntil = item.ValidUntil,
                    Reason = item.Reason,
                    IsActive = item.IsActive
                })
                .OrderBy(item => item.TargetName)
                .ToListAsync(ct);
            return (IReadOnlyList<PersonnelOverrideItem>)rows;
        }, cancellationToken);

        var effective = await _decisions.GetEffectiveAccessAsync(personnelId, DateTime.UtcNow, cancellationToken);
        return new PersonnelAuthorizationView
        {
            Groups = groups,
            Overrides = overrides,
            Effective = effective
        };
    }

    public Task AssignGroupAsync(PersonnelGroupEditModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        EnsureDates(model.ValidFrom, model.ValidUntil);
        var oldSnapshot = "";
        var created = false;
        return SaveWithAuditAsync(async (db, ct) =>
        {
            if (!await db.Personnel.AnyAsync(item => item.Id == model.PersonnelId, ct))
                throw new UnAccessException("Personel bulunamadı.");
            var group = await db.AccessGroups.FirstOrDefaultAsync(item => item.Id == model.AccessGroupId, ct)
                ?? throw new UnAccessException("Yetki grubu bulunamadı.");
            if (!group.IsActive)
                throw new UnAccessException("Pasif yetki grubu atanamaz.");

            var entity = await db.PersonnelAccessGroups.FirstOrDefaultAsync(
                item => item.PersonnelId == model.PersonnelId && item.AccessGroupId == model.AccessGroupId,
                ct);
            if (entity == null)
            {
                created = true;
                entity = new PersonnelAccessGroup
                {
                    PersonnelId = model.PersonnelId,
                    AccessGroupId = model.AccessGroupId
                };
                db.PersonnelAccessGroups.Add(entity);
            }
            else
            {
                oldSnapshot = AuditSnapshots.PersonnelAccessGroup(entity);
            }

            entity.ValidFrom = model.ValidFrom;
            entity.ValidUntil = model.ValidUntil;
            entity.IsActive = model.IsActive;
            return entity;
        }, saved => new AuditEntry
        {
            Action = created ? AuditActions.Created : saved.IsActive ? AuditActions.Updated : AuditActions.Deactivated,
            EntityType = AuditEntities.PersonnelAccessGroup,
            EntityId = saved.Id.ToString(),
            Description = $"Personele yetki grubu atandı: personel {saved.PersonnelId}, grup {saved.AccessGroupId}",
            OldValue = created ? null : oldSnapshot,
            NewValue = AuditSnapshots.PersonnelAccessGroup(saved)
        }, cancellationToken);
    }

    public Task RemoveGroupAsync(int personnelId, int accessGroupId, CancellationToken cancellationToken)
    {
        var oldSnapshot = "";
        return SaveWithAuditAsync(async (db, ct) =>
        {
            var entity = await db.PersonnelAccessGroups.FirstOrDefaultAsync(
                item => item.PersonnelId == personnelId && item.AccessGroupId == accessGroupId,
                ct) ?? throw new UnAccessException("Personelin bu yetki grubu ataması yok.");
            oldSnapshot = AuditSnapshots.PersonnelAccessGroup(entity);
            entity.IsActive = false;
            return entity;
        }, saved => new AuditEntry
        {
            Action = AuditActions.Deactivated,
            EntityType = AuditEntities.PersonnelAccessGroup,
            EntityId = saved.Id.ToString(),
            Description = $"Personelden yetki grubu kaldırıldı: personel {saved.PersonnelId}, grup {saved.AccessGroupId}",
            OldValue = oldSnapshot,
            NewValue = AuditSnapshots.PersonnelAccessGroup(saved)
        }, cancellationToken);
    }

    public Task SaveOverrideAsync(PersonnelOverrideEditModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var zoneId = model.ZoneId is > 0 ? model.ZoneId : null;
        var doorId = model.DoorId is > 0 ? model.DoorId : null;
        if ((zoneId == null) == (doorId == null))
            throw new UnAccessException("İstisna için bölge veya kapıdan yalnızca biri seçilmelidir.");
        EnsureDates(model.ValidFrom, model.ValidUntil);
        var reason = TextRules.Optional(model.Reason, "Gerekçe", FieldLengths.Description);
        var oldSnapshot = "";
        var created = model.Id <= 0;

        return SaveWithAuditAsync(async (db, ct) =>
        {
            if (!await db.Personnel.AnyAsync(item => item.Id == model.PersonnelId, ct))
                throw new UnAccessException("Personel bulunamadı.");
            if (zoneId is int selectedZone && !await db.Zones.AnyAsync(item => item.Id == selectedZone, ct))
                throw new UnAccessException("Bölge bulunamadı.");
            if (doorId is int selectedDoor && !await db.Doors.AnyAsync(item => item.Id == selectedDoor, ct))
                throw new UnAccessException("Kapı bulunamadı.");
            var schedule = await db.TimeSchedules.FirstOrDefaultAsync(item => item.Id == model.TimeScheduleId, ct)
                ?? throw new UnAccessException("Zaman planı bulunamadı.");
            if (!schedule.IsActive)
                throw new UnAccessException("Pasif zaman planı seçilemez.");

            PersonnelAccessOverride entity;
            if (model.Id > 0)
            {
                entity = await db.PersonnelAccessOverrides.FirstOrDefaultAsync(item => item.Id == model.Id && item.PersonnelId == model.PersonnelId, ct)
                    ?? throw new UnAccessException("Personel istisnası bulunamadı.");
                oldSnapshot = AuditSnapshots.PersonnelAccessOverride(entity);
            }
            else
            {
                entity = new PersonnelAccessOverride { PersonnelId = model.PersonnelId };
                db.PersonnelAccessOverrides.Add(entity);
            }

            entity.ZoneId = zoneId;
            entity.DoorId = doorId;
            entity.TimeScheduleId = model.TimeScheduleId;
            entity.IsAllowed = model.IsAllowed;
            entity.ValidFrom = model.ValidFrom;
            entity.ValidUntil = model.ValidUntil;
            entity.Reason = reason;
            entity.IsActive = model.IsActive;
            return entity;
        }, saved => new AuditEntry
        {
            Action = created ? AuditActions.Created : AuditActions.Updated,
            EntityType = AuditEntities.PersonnelAccessOverride,
            EntityId = saved.Id.ToString(),
            Description = created
                ? $"Kişisel istisna eklendi: personel {saved.PersonnelId}"
                : $"Kişisel istisna güncellendi: personel {saved.PersonnelId}",
            OldValue = created ? null : oldSnapshot,
            NewValue = AuditSnapshots.PersonnelAccessOverride(saved)
        }, cancellationToken);
    }

    private static void EnsureDates(DateOnly validFrom, DateOnly? validUntil)
    {
        if (validFrom == default)
            throw new UnAccessException("Başlangıç tarihi zorunludur.");
        if (validUntil is DateOnly until && until < validFrom)
            throw new UnAccessException("Bitiş tarihi başlangıçtan önce olamaz.");
    }
}
