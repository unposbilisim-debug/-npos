using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Audit;
using UnAccess.Core.Common;
using UnAccess.Core.Entities;
using UnAccess.Core.Enums;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Data.Services;

internal sealed class AccessGroupService : ServiceBase, IAccessGroupService
{
    public AccessGroupService(IDbContextFactory<UnAccessDbContext> factory, AuditWriter audit, ILogger<AccessGroupService> logger)
        : base(factory, audit, logger)
    {
    }

    public Task<IReadOnlyList<AccessGroupListItem>> ListAsync(CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var rows = await db.AccessGroups.AsNoTracking()
                .OrderBy(item => item.Name)
                .Select(item => new AccessGroupListItem
                {
                    Id = item.Id,
                    Code = item.Code,
                    Name = item.Name,
                    Description = item.Description,
                    IsActive = item.IsActive
                })
                .ToListAsync(ct);
            return (IReadOnlyList<AccessGroupListItem>)rows;
        }, cancellationToken);
    }

    public async Task<AccessGroupListItem> SaveAsync(int? id, AccessGroupEditModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var code = TextRules.Code(model.Code, "Grup kodu");
        var name = TextRules.Name(model.Name, "Grup adı");
        var description = TextRules.Optional(model.Description, "Açıklama", FieldLengths.Description);
        var oldSnapshot = "";
        var created = id is null;

        var saved = await SaveWithAuditAsync(async (db, ct) =>
        {
            var duplicate = await db.AccessGroups.AnyAsync(
                item => item.Code == code && (id == null || item.Id != id),
                ct);
            if (duplicate)
                throw new UnAccessException("Bu yetki grubu kodu zaten kayıtlı.");

            AccessGroup entity;
            if (id is int existingId)
            {
                entity = await db.AccessGroups.FirstOrDefaultAsync(item => item.Id == existingId, ct)
                    ?? throw new UnAccessException("Yetki grubu bulunamadı.");
                oldSnapshot = AuditSnapshots.AccessGroup(entity);
            }
            else
            {
                entity = new AccessGroup();
                db.AccessGroups.Add(entity);
            }

            entity.Code = code;
            entity.Name = name;
            entity.Description = description;
            entity.IsActive = model.IsActive;
            return entity;
        }, savedGroup => new AuditEntry
        {
            Action = created ? AuditActions.Created : savedGroup.IsActive ? AuditActions.Updated : AuditActions.Deactivated,
            EntityType = AuditEntities.AccessGroup,
            EntityId = savedGroup.Id.ToString(),
            Description = created
                ? $"Yetki grubu oluşturuldu: {savedGroup.Code}"
                : $"Yetki grubu güncellendi: {savedGroup.Code}",
            OldValue = created ? null : oldSnapshot,
            NewValue = AuditSnapshots.AccessGroup(savedGroup)
        }, cancellationToken);

        return new AccessGroupListItem
        {
            Id = saved.Id,
            Code = saved.Code,
            Name = saved.Name,
            Description = saved.Description,
            IsActive = saved.IsActive
        };
    }

    public Task<IReadOnlyList<AccessTreeNode>> GetTreeAsync(int accessGroupId, CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var buildings = await db.Buildings.AsNoTracking().OrderBy(item => item.Name).ToListAsync(ct);
            var floors = await db.Floors.AsNoTracking().OrderBy(item => item.FloorNumber).ToListAsync(ct);
            var zones = await db.Zones.AsNoTracking().OrderBy(item => item.Name).ToListAsync(ct);
            var doors = await db.Doors.AsNoTracking().OrderBy(item => item.Name).ToListAsync(ct);
            var zoneRules = await db.AccessGroupZones.AsNoTracking()
                .Where(item => item.AccessGroupId == accessGroupId && item.IsActive)
                .Join(db.TimeSchedules.AsNoTracking(), rule => rule.TimeScheduleId, schedule => schedule.Id, (rule, schedule) => new
                {
                    rule.Id,
                    rule.ZoneId,
                    rule.IsAllowed,
                    rule.TimeScheduleId,
                    schedule.Name
                })
                .ToListAsync(ct);
            var doorRules = await db.AccessGroupDoors.AsNoTracking()
                .Where(item => item.AccessGroupId == accessGroupId && item.IsActive)
                .Join(db.TimeSchedules.AsNoTracking(), rule => rule.TimeScheduleId, schedule => schedule.Id, (rule, schedule) => new
                {
                    rule.Id,
                    rule.DoorId,
                    rule.IsAllowed,
                    rule.TimeScheduleId,
                    schedule.Name
                })
                .ToListAsync(ct);

            var nodes = buildings.Select(building => new AccessTreeNode
            {
                Title = building.Name,
                Kind = "Building",
                Children = floors.Where(floor => floor.BuildingId == building.Id).Select(floor => new AccessTreeNode
                {
                    Title = floor.Name,
                    Kind = "Floor",
                    Children = zones.Where(zone => zone.FloorId == floor.Id).Select(zone =>
                    {
                        var zoneRule = zoneRules.FirstOrDefault(rule => rule.ZoneId == zone.Id);
                        return new AccessTreeNode
                        {
                            Title = zone.Name,
                            Kind = "Zone",
                            ZoneId = zone.Id,
                            RuleId = zoneRule?.Id,
                            IsAllowed = zoneRule?.IsAllowed,
                            TimeScheduleId = zoneRule?.TimeScheduleId,
                            ScheduleName = zoneRule?.Name,
                            Children = doors.Where(door => door.ZoneId == zone.Id).Select(door =>
                            {
                                var doorRule = doorRules.FirstOrDefault(rule => rule.DoorId == door.Id);
                                return new AccessTreeNode
                                {
                                    Title = door.Name,
                                    Kind = "Door",
                                    DoorId = door.Id,
                                    ZoneId = zone.Id,
                                    RuleId = doorRule?.Id,
                                    IsAllowed = doorRule?.IsAllowed,
                                    TimeScheduleId = doorRule?.TimeScheduleId,
                                    ScheduleName = doorRule?.Name
                                };
                            }).ToList()
                        };
                    }).ToList()
                }).ToList()
            }).ToList();
            return (IReadOnlyList<AccessTreeNode>)nodes;
        }, cancellationToken);
    }

    public Task SetZoneRuleAsync(int accessGroupId, int zoneId, int timeScheduleId, bool isAllowed, CancellationToken cancellationToken)
    {
        return SaveZoneAsync(accessGroupId, zoneId, timeScheduleId, isAllowed, clearing: false, cancellationToken);
    }

    public Task ClearZoneRuleAsync(int accessGroupId, int zoneId, CancellationToken cancellationToken)
    {
        return SaveZoneAsync(accessGroupId, zoneId, timeScheduleId: null, isAllowed: false, clearing: true, cancellationToken);
    }

    public Task SetDoorRuleAsync(int accessGroupId, int doorId, int timeScheduleId, bool isAllowed, CancellationToken cancellationToken)
    {
        return SaveDoorAsync(accessGroupId, doorId, timeScheduleId, isAllowed, clearing: false, cancellationToken);
    }

    public Task ClearDoorRuleAsync(int accessGroupId, int doorId, CancellationToken cancellationToken)
    {
        return SaveDoorAsync(accessGroupId, doorId, timeScheduleId: null, isAllowed: false, clearing: true, cancellationToken);
    }

    private Task SaveZoneAsync(
        int accessGroupId,
        int zoneId,
        int? timeScheduleId,
        bool isAllowed,
        bool clearing,
        CancellationToken cancellationToken)
    {
        var oldSnapshot = "";
        var created = false;
        return SaveWithAuditAsync(async (db, ct) =>
        {
            await RequireGroupAsync(db, accessGroupId, ct);
            if (!await db.Zones.AnyAsync(item => item.Id == zoneId, ct))
                throw new UnAccessException("Bölge bulunamadı.");
            if (!clearing)
                await RequireScheduleAsync(db, timeScheduleId!.Value, ct);

            var entity = await db.AccessGroupZones.FirstOrDefaultAsync(
                item => item.AccessGroupId == accessGroupId && item.ZoneId == zoneId,
                ct);
            if (entity == null)
            {
                if (clearing)
                    throw new UnAccessException("Kaldırılacak bölge yetkisi yok.");
                created = true;
                entity = new AccessGroupZone
                {
                    AccessGroupId = accessGroupId,
                    ZoneId = zoneId,
                    CreatedAt = DateTime.UtcNow
                };
                db.AccessGroupZones.Add(entity);
            }
            else
            {
                oldSnapshot = AuditSnapshots.AccessGroupZone(entity);
            }

            if (!clearing)
            {
                entity.TimeScheduleId = timeScheduleId!.Value;
                entity.IsAllowed = isAllowed;
                entity.IsActive = true;
            }
            else
            {
                entity.IsActive = false;
            }

            return entity;
        }, saved => new AuditEntry
        {
            Action = clearing ? AuditActions.Deactivated : created ? AuditActions.Created : AuditActions.Updated,
            EntityType = AuditEntities.AccessGroupZone,
            EntityId = saved.Id.ToString(),
            Description = clearing
                ? $"Grup bölge yetkisi kaldırıldı: grup {saved.AccessGroupId}, bölge {saved.ZoneId}"
                : $"Grup bölge yetkisi kaydedildi: grup {saved.AccessGroupId}, bölge {saved.ZoneId}",
            OldValue = created ? null : oldSnapshot,
            NewValue = AuditSnapshots.AccessGroupZone(saved)
        }, cancellationToken);
    }

    private Task SaveDoorAsync(
        int accessGroupId,
        int doorId,
        int? timeScheduleId,
        bool isAllowed,
        bool clearing,
        CancellationToken cancellationToken)
    {
        var oldSnapshot = "";
        var created = false;
        return SaveWithAuditAsync(async (db, ct) =>
        {
            await RequireGroupAsync(db, accessGroupId, ct);
            if (!await db.Doors.AnyAsync(item => item.Id == doorId, ct))
                throw new UnAccessException("Kapı bulunamadı.");
            if (!clearing)
                await RequireScheduleAsync(db, timeScheduleId!.Value, ct);

            var entity = await db.AccessGroupDoors.FirstOrDefaultAsync(
                item => item.AccessGroupId == accessGroupId && item.DoorId == doorId,
                ct);
            if (entity == null)
            {
                if (clearing)
                    throw new UnAccessException("Kaldırılacak kapı yetkisi yok.");
                created = true;
                entity = new AccessGroupDoor
                {
                    AccessGroupId = accessGroupId,
                    DoorId = doorId,
                    CreatedAt = DateTime.UtcNow
                };
                db.AccessGroupDoors.Add(entity);
            }
            else
            {
                oldSnapshot = AuditSnapshots.AccessGroupDoor(entity);
            }

            if (!clearing)
            {
                entity.TimeScheduleId = timeScheduleId!.Value;
                entity.IsAllowed = isAllowed;
                entity.IsActive = true;
            }
            else
            {
                entity.IsActive = false;
            }

            return entity;
        }, saved => new AuditEntry
        {
            Action = clearing ? AuditActions.Deactivated : created ? AuditActions.Created : AuditActions.Updated,
            EntityType = AuditEntities.AccessGroupDoor,
            EntityId = saved.Id.ToString(),
            Description = clearing
                ? $"Grup kapı yetkisi kaldırıldı: grup {saved.AccessGroupId}, kapı {saved.DoorId}"
                : $"Grup kapı yetkisi kaydedildi: grup {saved.AccessGroupId}, kapı {saved.DoorId}",
            OldValue = created ? null : oldSnapshot,
            NewValue = AuditSnapshots.AccessGroupDoor(saved)
        }, cancellationToken);
    }

    private static async Task RequireGroupAsync(UnAccessDbContext db, int accessGroupId, CancellationToken cancellationToken)
    {
        var group = await db.AccessGroups.AsNoTracking().FirstOrDefaultAsync(item => item.Id == accessGroupId, cancellationToken);
        if (group == null)
            throw new UnAccessException("Yetki grubu bulunamadı.");
        if (!group.IsActive)
            throw new UnAccessException("Pasif yetki grubuna kural eklenemez.");
    }

    private static async Task RequireScheduleAsync(UnAccessDbContext db, int timeScheduleId, CancellationToken cancellationToken)
    {
        var schedule = await db.TimeSchedules.AsNoTracking().FirstOrDefaultAsync(item => item.Id == timeScheduleId, cancellationToken);
        if (schedule == null)
            throw new UnAccessException("Zaman planı bulunamadı.");
        if (!schedule.IsActive)
            throw new UnAccessException("Pasif zaman planı seçilemez.");
    }
}

internal sealed class TimeScheduleService : ServiceBase, ITimeScheduleService
{
    public TimeScheduleService(IDbContextFactory<UnAccessDbContext> factory, AuditWriter audit, ILogger<TimeScheduleService> logger)
        : base(factory, audit, logger)
    {
    }

    public Task<IReadOnlyList<TimeScheduleListItem>> ListAsync(CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var rows = await db.TimeSchedules.AsNoTracking()
                .OrderBy(item => item.Name)
                .Select(item => new TimeScheduleListItem
                {
                    Id = item.Id,
                    Code = item.Code,
                    Name = item.Name,
                    Description = item.Description,
                    IsActive = item.IsActive
                })
                .ToListAsync(ct);
            return (IReadOnlyList<TimeScheduleListItem>)rows;
        }, cancellationToken);
    }

    public Task<TimeScheduleEditModel?> GetAsync(int id, CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var schedule = await db.TimeSchedules.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
            if (schedule == null)
                return null;

            var intervals = await db.TimeScheduleDetails.AsNoTracking()
                .Where(item => item.TimeScheduleId == id && item.IsActive)
                .OrderBy(item => item.DayOfWeek)
                .ThenBy(item => item.StartTime)
                .Select(item => new TimeIntervalModel
                {
                    Id = item.Id,
                    DayOfWeek = item.DayOfWeek,
                    StartTime = item.StartTime,
                    EndTime = item.EndTime
                })
                .ToListAsync(ct);
            return new TimeScheduleEditModel
            {
                Id = schedule.Id,
                Code = schedule.Code,
                Name = schedule.Name,
                Description = schedule.Description,
                IsActive = schedule.IsActive,
                Intervals = intervals
            };
        }, cancellationToken);
    }

    public async Task<TimeScheduleListItem> SaveAsync(TimeScheduleEditModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var code = TextRules.Code(model.Code, "Plan kodu");
        var name = TextRules.Name(model.Name, "Plan adı");
        var description = TextRules.Optional(model.Description, "Açıklama", FieldLengths.Description);
        var oldSnapshot = "";
        var created = model.Id <= 0;

        var saved = await SaveWithAuditAsync(async (db, ct) =>
        {
            var duplicate = await db.TimeSchedules.AnyAsync(
                item => item.Code == code && (model.Id <= 0 || item.Id != model.Id),
                ct);
            if (duplicate)
                throw new UnAccessException("Bu zaman planı kodu zaten kayıtlı.");

            TimeSchedule entity;
            if (model.Id > 0)
            {
                entity = await db.TimeSchedules.FirstOrDefaultAsync(item => item.Id == model.Id, ct)
                    ?? throw new UnAccessException("Zaman planı bulunamadı.");
                var existing = await db.TimeScheduleDetails.Where(item => item.TimeScheduleId == entity.Id).ToListAsync(ct);
                oldSnapshot = AuditSnapshots.TimeSchedule(entity, existing.Where(item => item.IsActive).Select(Describe));
                var incoming = model.Intervals.Where(item => item.Id > 0).Select(item => item.Id).ToHashSet();
                foreach (var detail in existing)
                    detail.IsActive = incoming.Contains(detail.Id);
            }
            else
            {
                entity = new TimeSchedule();
                db.TimeSchedules.Add(entity);
            }

            entity.Code = code;
            entity.Name = name;
            entity.Description = description;
            entity.IsActive = model.IsActive;

            foreach (var interval in model.Intervals)
            {
                TimeScheduleDetail detail;
                if (interval.Id > 0)
                {
                    detail = await db.TimeScheduleDetails.FirstOrDefaultAsync(
                        item => item.Id == interval.Id && item.TimeScheduleId == entity.Id,
                        ct) ?? throw new UnAccessException("Zaman aralığı bulunamadı.");
                }
                else
                {
                    detail = new TimeScheduleDetail { TimeSchedule = entity };
                    db.TimeScheduleDetails.Add(detail);
                }

                detail.DayOfWeek = interval.DayOfWeek;
                detail.StartTime = interval.StartTime;
                detail.EndTime = interval.EndTime;
                detail.IsActive = true;
            }

            return entity;
        }, schedule => new AuditEntry
        {
            Action = created ? AuditActions.Created : AuditActions.Updated,
            EntityType = AuditEntities.TimeSchedule,
            EntityId = schedule.Id.ToString(),
            Description = created ? $"Zaman planı oluşturuldu: {schedule.Code}" : $"Zaman planı güncellendi: {schedule.Code}",
            OldValue = created ? null : oldSnapshot,
            NewValue = AuditSnapshots.TimeSchedule(schedule, model.Intervals.Select(item =>
                $"{TurkishCalendar.Day(item.DayOfWeek)} {item.StartTime:HH:mm}-{item.EndTime:HH:mm}"))
        }, cancellationToken);

        return new TimeScheduleListItem
        {
            Id = saved.Id,
            Code = saved.Code,
            Name = saved.Name,
            Description = saved.Description,
            IsActive = saved.IsActive
        };
    }

    private static string Describe(TimeScheduleDetail detail) =>
        $"{TurkishCalendar.Day(detail.DayOfWeek)} {detail.StartTime:HH:mm}-{detail.EndTime:HH:mm}";
}
