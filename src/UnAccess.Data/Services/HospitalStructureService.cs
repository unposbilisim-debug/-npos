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

internal sealed class HospitalStructureService : ServiceBase, IHospitalStructureService
{
    private readonly IDeviceSyncOutbox _outbox;

    public HospitalStructureService(
        IDbContextFactory<UnAccessDbContext> factory,
        AuditWriter audit,
        ILogger<HospitalStructureService> logger,
        IDeviceSyncOutbox outbox)
        : base(factory, audit, logger)
    {
        _outbox = outbox;
    }

    public Task<IReadOnlyList<BuildingListItem>> ListBuildingsAsync(CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var rows = await db.Buildings.AsNoTracking()
                .OrderBy(b => b.Code)
                .Select(b => new BuildingListItem { Id = b.Id, Code = b.Code, Name = b.Name, IsActive = b.IsActive })
                .ToListAsync(ct);
            return (IReadOnlyList<BuildingListItem>)rows;
        }, cancellationToken);
    }

    public Task<BuildingListItem?> GetBuildingAsync(int id, CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
            await db.Buildings.AsNoTracking()
                .Where(b => b.Id == id)
                .Select(b => new BuildingListItem { Id = b.Id, Code = b.Code, Name = b.Name, IsActive = b.IsActive })
                .FirstOrDefaultAsync(ct), cancellationToken);
    }

    public async Task<BuildingListItem> CreateBuildingAsync(BuildingEditModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var entity = await SaveWithAuditAsync(async (db, ct) =>
        {
            var code = TextRules.Code(model.Code, "Bina kodu");
            var name = TextRules.Name(model.Name, "Bina adı");
            if (await db.Buildings.AnyAsync(b => b.Code == code, ct))
                throw new UnAccessException("Bu bina kodu zaten kayıtlı.");

            var building = new Building { Code = code, Name = name, IsActive = model.IsActive };
            db.Buildings.Add(building);
            return building;
        }, saved => AuditFor("Bina", AuditEntities.Building, saved.Id, saved.Code, saved.Name, null, AuditSnapshots.Building(saved), saved.IsActive, wasActive: null), cancellationToken);

        return new BuildingListItem
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            IsActive = entity.IsActive
        };
    }

    public async Task<BuildingListItem> UpdateBuildingAsync(int id, BuildingEditModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var oldSnapshot = "";
        bool? wasActive = null;
        var entity = await SaveWithAuditAsync(async (db, ct) =>
        {
            var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == id, ct);
            if (building == null)
                throw new UnAccessException("Bina bulunamadı.");

            var code = TextRules.Code(model.Code, "Bina kodu");
            var name = TextRules.Name(model.Name, "Bina adı");
            if (await db.Buildings.AnyAsync(b => b.Code == code && b.Id != building.Id, ct))
                throw new UnAccessException("Bu bina kodu zaten kayıtlı.");

            oldSnapshot = AuditSnapshots.Building(building);
            wasActive = building.IsActive;
            building.Code = code;
            building.Name = name;
            building.IsActive = model.IsActive;
            return building;
        }, saved => AuditFor("Bina", AuditEntities.Building, saved.Id, saved.Code, saved.Name, oldSnapshot, AuditSnapshots.Building(saved), saved.IsActive, wasActive), cancellationToken);

        return new BuildingListItem { Id = entity.Id, Code = entity.Code, Name = entity.Name, IsActive = entity.IsActive };
    }

    public Task<IReadOnlyList<FloorListItem>> ListFloorsAsync(int? buildingId, CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var query =
                from floor in db.Floors.AsNoTracking()
                join building in db.Buildings.AsNoTracking() on floor.BuildingId equals building.Id
                select new { floor, building };
            if (buildingId is int id)
                query = query.Where(x => x.floor.BuildingId == id);

            var rows = await query
                .OrderBy(x => x.building.Code)
                .ThenBy(x => x.floor.FloorNumber)
                .Select(x => new FloorListItem
                {
                    Id = x.floor.Id,
                    BuildingId = x.building.Id,
                    BuildingName = x.building.Name,
                    Code = x.floor.Code,
                    Name = x.floor.Name,
                    FloorNumber = x.floor.FloorNumber,
                    IsActive = x.floor.IsActive
                })
                .ToListAsync(ct);
            return (IReadOnlyList<FloorListItem>)rows;
        }, cancellationToken);
    }

    public Task<FloorListItem?> GetFloorAsync(int id, CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
            await (
                from floor in db.Floors.AsNoTracking()
                join building in db.Buildings.AsNoTracking() on floor.BuildingId equals building.Id
                where floor.Id == id
                select new FloorListItem
                {
                    Id = floor.Id,
                    BuildingId = building.Id,
                    BuildingName = building.Name,
                    Code = floor.Code,
                    Name = floor.Name,
                    FloorNumber = floor.FloorNumber,
                    IsActive = floor.IsActive
                }).FirstOrDefaultAsync(ct), cancellationToken);
    }

    public async Task<FloorListItem> CreateFloorAsync(FloorEditModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var buildingName = "";
        var entity = await SaveWithAuditAsync(async (db, ct) =>
        {
            var building = await RequireBuildingAsync(db, model.BuildingId, ct);
            var code = TextRules.Code(model.Code, "Kat kodu");
            var name = TextRules.Name(model.Name, "Kat adı");
            EnsureFloorNumber(model.FloorNumber);
            await EnsureFloorAvailableAsync(db, building.Id, code, model.FloorNumber, excludeId: 0, ct);
            buildingName = building.Name;
            var floor = new Floor
            {
                BuildingId = building.Id,
                Code = code,
                Name = name,
                FloorNumber = model.FloorNumber,
                IsActive = model.IsActive
            };
            db.Floors.Add(floor);
            return floor;
        }, saved => AuditFor("Kat", AuditEntities.Floor, saved.Id, saved.Code, saved.Name, null, AuditSnapshots.Floor(saved), saved.IsActive, wasActive: null), cancellationToken);

        return new FloorListItem
        {
            Id = entity.Id,
            BuildingId = entity.BuildingId,
            BuildingName = buildingName,
            Code = entity.Code,
            Name = entity.Name,
            FloorNumber = entity.FloorNumber,
            IsActive = entity.IsActive
        };
    }

    public async Task<FloorListItem> UpdateFloorAsync(int id, FloorEditModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var buildingName = "";
        var oldSnapshot = "";
        bool? wasActive = null;
        var entity = await SaveWithAuditAsync(async (db, ct) =>
        {
            var floor = await db.Floors.FirstOrDefaultAsync(f => f.Id == id, ct);
            if (floor == null)
                throw new UnAccessException("Kat bulunamadı.");

            var building = await RequireBuildingAsync(db, model.BuildingId, ct);
            var code = TextRules.Code(model.Code, "Kat kodu");
            var name = TextRules.Name(model.Name, "Kat adı");
            EnsureFloorNumber(model.FloorNumber);
            await EnsureFloorAvailableAsync(db, building.Id, code, model.FloorNumber, floor.Id, ct);
            oldSnapshot = AuditSnapshots.Floor(floor);
            wasActive = floor.IsActive;
            buildingName = building.Name;
            floor.BuildingId = building.Id;
            floor.Code = code;
            floor.Name = name;
            floor.FloorNumber = model.FloorNumber;
            floor.IsActive = model.IsActive;
            return floor;
        }, saved => AuditFor("Kat", AuditEntities.Floor, saved.Id, saved.Code, saved.Name, oldSnapshot, AuditSnapshots.Floor(saved), saved.IsActive, wasActive), cancellationToken);

        return new FloorListItem
        {
            Id = entity.Id,
            BuildingId = entity.BuildingId,
            BuildingName = buildingName,
            Code = entity.Code,
            Name = entity.Name,
            FloorNumber = entity.FloorNumber,
            IsActive = entity.IsActive
        };
    }

    public Task<IReadOnlyList<ZoneListItem>> ListZonesAsync(int? buildingId, int? floorId, CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var query =
                from zone in db.Zones.AsNoTracking()
                join floor in db.Floors.AsNoTracking() on zone.FloorId equals floor.Id
                join building in db.Buildings.AsNoTracking() on floor.BuildingId equals building.Id
                select new { zone, floor, building };
            if (buildingId is int selectedBuilding)
                query = query.Where(x => x.building.Id == selectedBuilding);
            if (floorId is int selectedFloor)
                query = query.Where(x => x.floor.Id == selectedFloor);

            var rows = await query
                .OrderBy(x => x.building.Code)
                .ThenBy(x => x.floor.FloorNumber)
                .ThenBy(x => x.zone.Code)
                .Select(x => new ZoneListItem
                {
                    Id = x.zone.Id,
                    FloorId = x.floor.Id,
                    Location = x.building.Name + " / " + x.floor.Name,
                    Code = x.zone.Code,
                    Name = x.zone.Name,
                    IsCritical = x.zone.IsCritical,
                    IsActive = x.zone.IsActive
                })
                .ToListAsync(ct);
            return (IReadOnlyList<ZoneListItem>)rows;
        }, cancellationToken);
    }

    public Task<ZoneListItem?> GetZoneAsync(int id, CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
            await (
                from zone in db.Zones.AsNoTracking()
                join floor in db.Floors.AsNoTracking() on zone.FloorId equals floor.Id
                join building in db.Buildings.AsNoTracking() on floor.BuildingId equals building.Id
                where zone.Id == id
                select new ZoneListItem
                {
                    Id = zone.Id,
                    FloorId = floor.Id,
                    Location = building.Name + " / " + floor.Name,
                    Code = zone.Code,
                    Name = zone.Name,
                    IsCritical = zone.IsCritical,
                    IsActive = zone.IsActive
                }).FirstOrDefaultAsync(ct), cancellationToken);
    }

    public async Task<ZoneListItem> CreateZoneAsync(ZoneEditModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var location = "";
        var entity = await SaveWithAuditAsync(async (db, ct) =>
        {
            var floor = await RequireFloorAsync(db, model.FloorId, ct);
            var code = TextRules.Code(model.Code, "Bölge kodu");
            var name = TextRules.Name(model.Name, "Bölge adı");
            if (await db.Zones.AnyAsync(z => z.FloorId == floor.Id && z.Code == code, ct))
                throw new UnAccessException("Bu bölge kodu aynı katta zaten kayıtlı.");

            location = floor.Building.Name + " / " + floor.Name;
            var zone = new Zone
            {
                FloorId = floor.Id,
                Code = code,
                Name = name,
                IsCritical = model.IsCritical,
                IsActive = model.IsActive
            };
            db.Zones.Add(zone);
            return zone;
        }, saved => AuditFor("Bölge", AuditEntities.Zone, saved.Id, saved.Code, saved.Name, null, AuditSnapshots.Zone(saved), saved.IsActive, wasActive: null), cancellationToken);

        return new ZoneListItem
        {
            Id = entity.Id,
            FloorId = entity.FloorId,
            Location = location,
            Code = entity.Code,
            Name = entity.Name,
            IsCritical = entity.IsCritical,
            IsActive = entity.IsActive
        };
    }

    public async Task<ZoneListItem> UpdateZoneAsync(int id, ZoneEditModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var location = "";
        var oldSnapshot = "";
        bool? wasActive = null;
        var entity = await SaveWithAuditAsync(async (db, ct) =>
        {
            var zone = await db.Zones.FirstOrDefaultAsync(z => z.Id == id, ct);
            if (zone == null)
                throw new UnAccessException("Bölge bulunamadı.");

            var floor = await RequireFloorAsync(db, model.FloorId, ct);
            var code = TextRules.Code(model.Code, "Bölge kodu");
            var name = TextRules.Name(model.Name, "Bölge adı");
            if (await db.Zones.AnyAsync(z => z.FloorId == floor.Id && z.Code == code && z.Id != zone.Id, ct))
                throw new UnAccessException("Bu bölge kodu aynı katta zaten kayıtlı.");

            oldSnapshot = AuditSnapshots.Zone(zone);
            wasActive = zone.IsActive;
            location = floor.Building.Name + " / " + floor.Name;
            zone.FloorId = floor.Id;
            zone.Code = code;
            zone.Name = name;
            zone.IsCritical = model.IsCritical;
            zone.IsActive = model.IsActive;
            return zone;
        }, saved => AuditFor("Bölge", AuditEntities.Zone, saved.Id, saved.Code, saved.Name, oldSnapshot, AuditSnapshots.Zone(saved), saved.IsActive, wasActive), cancellationToken);

        return new ZoneListItem
        {
            Id = entity.Id,
            FloorId = entity.FloorId,
            Location = location,
            Code = entity.Code,
            Name = entity.Name,
            IsCritical = entity.IsCritical,
            IsActive = entity.IsActive
        };
    }

    public Task<IReadOnlyList<DoorListItem>> ListDoorsAsync(int? zoneId, CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var query =
                from door in db.Doors.AsNoTracking()
                join zone in db.Zones.AsNoTracking() on door.ZoneId equals zone.Id
                join floor in db.Floors.AsNoTracking() on zone.FloorId equals floor.Id
                join building in db.Buildings.AsNoTracking() on floor.BuildingId equals building.Id
                join controller in db.Controllers.AsNoTracking() on door.ControllerId equals controller.Id into controllerJoin
                from controller in controllerJoin.DefaultIfEmpty()
                select new { door, zone, floor, building, controller };
            if (zoneId is int id)
                query = query.Where(x => x.door.ZoneId == id);

            var rows = await query
                .OrderBy(x => x.building.Code)
                .ThenBy(x => x.floor.FloorNumber)
                .ThenBy(x => x.zone.Code)
                .ThenBy(x => x.door.Code)
                .Select(x => new DoorListItem
                {
                    Id = x.door.Id,
                    Code = x.door.Code,
                    Name = x.door.Name,
                    Location = x.building.Name + " / " + x.floor.Name + " / " + x.zone.Name,
                    ChannelNumber = x.door.ChannelNumber,
                    ControllerName = x.controller != null ? x.controller.Name : null,
                    IsCritical = x.door.IsCritical,
                    IsActive = x.door.IsActive
                })
                .ToListAsync(ct);
            return (IReadOnlyList<DoorListItem>)rows;
        }, cancellationToken);
    }

    public Task<DoorEditModel?> GetDoorAsync(int id, CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var door = await db.Doors.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
            if (door == null)
                return null;

            return new DoorEditModel
            {
                ZoneId = door.ZoneId,
                Code = door.Code,
                Name = door.Name,
                ControllerId = door.ControllerId,
                ChannelNumber = door.ChannelNumber,
                EntryReaderName = door.EntryReaderName,
                ExitReaderName = door.ExitReaderName,
                HasExitReader = door.HasExitReader,
                IsCritical = door.IsCritical,
                IsActive = door.IsActive
            };
        }, cancellationToken);
    }

    public async Task<DoorListItem> CreateDoorAsync(DoorEditModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var location = "";
        string? controllerName = null;
        var entity = await SaveWithAuditAsync(async (db, ct) =>
        {
            var prepared = await PrepareDoorAsync(db, model, excludeId: 0, ct);
            location = prepared.Location;
            controllerName = prepared.ControllerName;
            var door = prepared.Door;
            db.Doors.Add(door);
            if (door.ControllerId is int controllerId)
                await _outbox.EnqueueFullSyncAsync(db, controllerId, ct);
            return door;
        }, saved => AuditFor("Kapı", AuditEntities.Door, saved.Id, saved.Code, saved.Name, null, AuditSnapshots.Door(saved), saved.IsActive, wasActive: null), cancellationToken);

        return ToDoorItem(entity, location, controllerName);
    }

    public async Task<DoorListItem> UpdateDoorAsync(int id, DoorEditModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var location = "";
        string? controllerName = null;
        var oldSnapshot = "";
        bool? wasActive = null;
        var entity = await SaveWithAuditAsync(async (db, ct) =>
        {
            var door = await db.Doors.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (door == null)
                throw new UnAccessException("Kapı bulunamadı.");

            var prepared = await PrepareDoorAsync(db, model, door.Id, ct);
            location = prepared.Location;
            controllerName = prepared.ControllerName;
            oldSnapshot = AuditSnapshots.Door(door);
            wasActive = door.IsActive;
            var previousController = door.ControllerId;
            var previousChannel = door.ChannelNumber;
            var previousActive = door.IsActive;
            door.ZoneId = prepared.Door.ZoneId;
            door.Code = prepared.Door.Code;
            door.Name = prepared.Door.Name;
            door.ControllerId = prepared.Door.ControllerId;
            door.ChannelNumber = prepared.Door.ChannelNumber;
            door.EntryReaderName = prepared.Door.EntryReaderName;
            door.ExitReaderName = prepared.Door.ExitReaderName;
            door.HasExitReader = prepared.Door.HasExitReader;
            door.IsCritical = prepared.Door.IsCritical;
            door.IsActive = prepared.Door.IsActive;
            if (previousController != door.ControllerId || previousChannel != door.ChannelNumber || previousActive != door.IsActive)
            {
                if (previousController is int oldController)
                    await _outbox.EnqueueFullSyncAsync(db, oldController, ct);
                if (door.ControllerId is int newController && newController != previousController)
                    await _outbox.EnqueueFullSyncAsync(db, newController, ct);
            }

            return door;
        }, saved => AuditFor("Kapı", AuditEntities.Door, saved.Id, saved.Code, saved.Name, oldSnapshot, AuditSnapshots.Door(saved), saved.IsActive, wasActive), cancellationToken);

        return ToDoorItem(entity, location, controllerName);
    }

    public Task<IReadOnlyList<LookupItem>> GetBuildingOptionsAsync(CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var rows = await db.Buildings.AsNoTracking()
                .OrderBy(b => b.Name)
                .Select(b => new LookupItem { Id = b.Id, Code = b.Code, Name = b.Name, IsActive = b.IsActive })
                .ToListAsync(ct);
            return (IReadOnlyList<LookupItem>)rows;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<FloorOption>> GetFloorOptionsAsync(int? buildingId, CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var query =
                from floor in db.Floors.AsNoTracking()
                join building in db.Buildings.AsNoTracking() on floor.BuildingId equals building.Id
                select new { floor, building };
            if (buildingId is int id)
                query = query.Where(x => x.building.Id == id);

            var rows = await query
                .OrderBy(x => x.building.Name)
                .ThenBy(x => x.floor.FloorNumber)
                .Select(x => new FloorOption
                {
                    Id = x.floor.Id,
                    BuildingId = x.building.Id,
                    Display = x.building.Name + " / " + x.floor.Name,
                    IsActive = x.floor.IsActive && x.building.IsActive
                })
                .ToListAsync(ct);
            return (IReadOnlyList<FloorOption>)rows;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<ZoneOption>> GetZoneOptionsAsync(CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var rows = await (
                from zone in db.Zones.AsNoTracking()
                join floor in db.Floors.AsNoTracking() on zone.FloorId equals floor.Id
                join building in db.Buildings.AsNoTracking() on floor.BuildingId equals building.Id
                orderby building.Name, floor.FloorNumber, zone.Name
                select new ZoneOption
                {
                    Id = zone.Id,
                    FloorId = floor.Id,
                    Display = building.Name + " / " + floor.Name + " / " + zone.Name,
                    IsActive = zone.IsActive && floor.IsActive && building.IsActive
                }).ToListAsync(ct);
            return (IReadOnlyList<ZoneOption>)rows;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<ControllerOption>> GetControllerOptionsAsync(CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var rows = await db.Controllers.AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new ControllerOption
                {
                    Id = c.Id,
                    Display = c.Code + " - " + c.Name,
                    IsActive = c.IsActive
                })
                .ToListAsync(ct);
            return (IReadOnlyList<ControllerOption>)rows;
        }, cancellationToken);
    }

    private static async Task<Building> RequireBuildingAsync(UnAccessDbContext db, int buildingId, CancellationToken cancellationToken)
    {
        if (buildingId <= 0)
            throw new UnAccessException("Bina seçiniz.");
        var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId, cancellationToken);
        if (building == null)
            throw new UnAccessException("Bina bulunamadı.");
        return building;
    }

    private static async Task<Floor> RequireFloorAsync(UnAccessDbContext db, int floorId, CancellationToken cancellationToken)
    {
        if (floorId <= 0)
            throw new UnAccessException("Kat seçiniz.");
        var floor = await db.Floors.Include(f => f.Building).FirstOrDefaultAsync(f => f.Id == floorId, cancellationToken);
        if (floor == null)
            throw new UnAccessException("Kat bulunamadı.");
        return floor;
    }

    private static void EnsureFloorNumber(int floorNumber)
    {
        if (floorNumber is < -20 or > 200)
            throw new UnAccessException("Kat numarası -20 ile 200 arasında olmalıdır.");
    }

    private static async Task EnsureFloorAvailableAsync(
        UnAccessDbContext db,
        int buildingId,
        string code,
        int floorNumber,
        int excludeId,
        CancellationToken cancellationToken)
    {
        if (await db.Floors.AnyAsync(f => f.BuildingId == buildingId && f.Code == code && f.Id != excludeId, cancellationToken))
            throw new UnAccessException("Bu kat kodu aynı binada zaten kayıtlı.");
        if (await db.Floors.AnyAsync(f => f.BuildingId == buildingId && f.FloorNumber == floorNumber && f.Id != excludeId, cancellationToken))
            throw new UnAccessException("Bu kat numarası aynı binada zaten kayıtlı.");
    }

    private static async Task<PreparedDoor> PrepareDoorAsync(
        UnAccessDbContext db,
        DoorEditModel model,
        int excludeId,
        CancellationToken cancellationToken)
    {
        if (model.ZoneId <= 0)
            throw new UnAccessException("Bölge seçiniz.");

        var zone = await db.Zones
            .Include(z => z.Floor)
            .ThenInclude(f => f.Building)
            .FirstOrDefaultAsync(z => z.Id == model.ZoneId, cancellationToken);
        if (zone == null)
            throw new UnAccessException("Bölge bulunamadı.");

        var code = TextRules.Code(model.Code, "Kapı kodu");
        var name = TextRules.Name(model.Name, "Kapı adı");
        if (await db.Doors.AnyAsync(d => d.Code == code && d.Id != excludeId, cancellationToken))
            throw new UnAccessException("Bu kapı kodu zaten kayıtlı.");
        if (model.ChannelNumber is < 0 or > 128)
            throw new UnAccessException("Kanal numarası 0 ile 128 arasında olmalıdır.");

        string? controllerName = null;
        int? controllerId = model.ControllerId is > 0 ? model.ControllerId : null;
        if (controllerId is int selectedController)
        {
            if (model.ChannelNumber < 1)
                throw new UnAccessException("Kontrolör seçildiğinde kanal numarası en az 1 olmalıdır.");
            controllerName = await db.Controllers
                .Where(c => c.Id == selectedController)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);
            if (controllerName == null)
                throw new UnAccessException("Kontrolör bulunamadı.");
            if (model.IsActive && await db.Doors.AnyAsync(
                door => door.IsActive
                    && door.ControllerId == controllerId
                    && door.ChannelNumber == model.ChannelNumber
                    && door.Id != excludeId,
                cancellationToken))
                throw new UnAccessException("Bu kanal aynı kontrolörde başka bir aktif kapıya atanmış.");
        }

        var hasExitReader = model.HasExitReader;
        return new PreparedDoor(
            new Door
            {
                ZoneId = zone.Id,
                Code = code,
                Name = name,
                ControllerId = controllerId,
                ChannelNumber = controllerId == null ? model.ChannelNumber : model.ChannelNumber,
                EntryReaderName = TextRules.Optional(model.EntryReaderName, "Giriş okuyucu", FieldLengths.ReaderName),
                ExitReaderName = hasExitReader
                    ? TextRules.Optional(model.ExitReaderName, "Çıkış okuyucu", FieldLengths.ReaderName)
                    : null,
                HasExitReader = hasExitReader,
                IsCritical = model.IsCritical,
                IsActive = model.IsActive
            },
            zone.Floor.Building.Name + " / " + zone.Floor.Name + " / " + zone.Name,
            controllerName);
    }

    private static DoorListItem ToDoorItem(Door door, string location, string? controllerName) => new()
    {
        Id = door.Id,
        Code = door.Code,
        Name = door.Name,
        Location = location,
        ChannelNumber = door.ChannelNumber,
        ControllerName = controllerName,
        IsCritical = door.IsCritical,
        IsActive = door.IsActive
    };

    private static AuditEntry AuditFor(
        string label,
        string entityType,
        int id,
        string code,
        string name,
        string? oldValue,
        string newValue,
        bool isActive,
        bool? wasActive)
    {
        string action;
        if (wasActive == null)
            action = AuditActions.Created;
        else if (wasActive.Value == isActive)
            action = AuditActions.Updated;
        else
            action = isActive ? AuditActions.Activated : AuditActions.Deactivated;

        var description = action switch
        {
            AuditActions.Created => $"{label} oluşturuldu: {code} {name}",
            AuditActions.Activated => $"{label} aktifleştirildi: {code} {name}",
            AuditActions.Deactivated => $"{label} pasife alındı: {code} {name}",
            _ => $"{label} güncellendi: {code} {name}"
        };

        return new AuditEntry
        {
            Action = action,
            EntityType = entityType,
            EntityId = id.ToString(),
            Description = description,
            OldValue = oldValue,
            NewValue = newValue
        };
    }

    private sealed record PreparedDoor(Door Door, string Location, string? ControllerName);
}
