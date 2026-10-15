using Microsoft.EntityFrameworkCore;
using UnAccess.Core.Entities;
using UnAccess.Core.Enums;

namespace UnAccess.Data.Seeding;

public static class DevelopmentSeeder
{
    public static async Task SeedAsync(UnAccessDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var departments = new Dictionary<string, Department>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, name) in Departments)
            departments[code] = await EnsureDepartmentAsync(db, code, name, cancellationToken);

        var jobs = new Dictionary<string, JobTitle>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, name) in JobTitles)
            jobs[code] = await EnsureJobTitleAsync(db, code, name, cancellationToken);

        await EnsurePersonnelAsync(db, departments, jobs, cancellationToken);
        await EnsureHospitalAsync(db, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<Department> EnsureDepartmentAsync(
        UnAccessDbContext db,
        string code,
        string name,
        CancellationToken cancellationToken)
    {
        var existing = await db.Departments.FirstOrDefaultAsync(d => d.Code == code, cancellationToken);
        if (existing != null)
            return existing;

        var department = new Department { Code = code, Name = name, IsActive = true };
        db.Departments.Add(department);
        await db.SaveChangesAsync(cancellationToken);
        return department;
    }

    private static async Task<JobTitle> EnsureJobTitleAsync(
        UnAccessDbContext db,
        string code,
        string name,
        CancellationToken cancellationToken)
    {
        var existing = await db.JobTitles.FirstOrDefaultAsync(j => j.Code == code, cancellationToken);
        if (existing != null)
            return existing;

        var jobTitle = new JobTitle { Code = code, Name = name, IsActive = true };
        db.JobTitles.Add(jobTitle);
        await db.SaveChangesAsync(cancellationToken);
        return jobTitle;
    }

    private static async Task EnsurePersonnelAsync(
        UnAccessDbContext db,
        IReadOnlyDictionary<string, Department> departments,
        IReadOnlyDictionary<string, JobTitle> jobs,
        CancellationToken cancellationToken)
    {
        foreach (var sample in People)
        {
            var person = await db.Personnel.FirstOrDefaultAsync(p => p.PersonnelNumber == sample.Number, cancellationToken);
            if (person == null)
            {
                person = new Personnel
                {
                    PersonnelNumber = sample.Number,
                    FirstName = sample.FirstName,
                    LastName = sample.LastName,
                    DepartmentId = departments[sample.DepartmentCode].Id,
                    JobTitleId = jobs[sample.JobCode].Id,
                    Phone = sample.Phone,
                    Email = sample.Email,
                    IsActive = sample.IsActive
                };
                db.Personnel.Add(person);
                await db.SaveChangesAsync(cancellationToken);
            }

            foreach (var card in sample.Cards)
            {
                if (await db.Cards.AnyAsync(c => c.CardNumber == card.Number, cancellationToken))
                    continue;

                db.Cards.Add(new Card
                {
                    PersonnelId = person.Id,
                    CardNumber = card.Number,
                    Status = card.Status,
                    ValidFrom = card.ValidFrom,
                    ValidUntil = card.ValidUntil,
                    Description = card.Description
                });
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private static async Task EnsureHospitalAsync(UnAccessDbContext db, CancellationToken cancellationToken)
    {
        var controller = await db.Controllers.FirstOrDefaultAsync(c => c.Code == "SIM-ASC-01", cancellationToken);
        if (controller == null)
        {
            controller = new Controller
            {
                Code = "SIM-ASC-01",
                Name = "Test Kontrolörü",
                Brand = "Dahua",
                Model = "Simulator",
                IpAddress = "127.0.0.1",
                Port = 15050,
                SerialNumber = "SIM-0001",
                IsActive = true,
                LastSeenAt = null
            };
            db.Controllers.Add(controller);
            await db.SaveChangesAsync(cancellationToken);
        }

        var a = await EnsureBuildingAsync(db, "A", "A BLOK", cancellationToken);
        var b = await EnsureBuildingAsync(db, "B", "B BLOK", cancellationToken);
        var c = await EnsureBuildingAsync(db, "C", "C BLOK", cancellationToken);

        var aGround = await EnsureFloorAsync(db, a.Id, "Z", "Zemin Kat", 0, cancellationToken);
        var aFirst = await EnsureFloorAsync(db, a.Id, "1", "1. Kat", 1, cancellationToken);
        var bSecond = await EnsureFloorAsync(db, b.Id, "2", "2. Kat", 2, cancellationToken);
        var cFirst = await EnsureFloorAsync(db, c.Id, "1", "1. Kat", 1, cancellationToken);

        await EnsureZoneAsync(db, aGround.Id, "GIRIS", "Ana Giriş", isCritical: false, cancellationToken);
        await EnsureZoneAsync(db, aGround.Id, "ACIL", "Acil Servis", isCritical: false, cancellationToken);
        await EnsureZoneAsync(db, aFirst.Id, "POL", "Poliklinikler", isCritical: false, cancellationToken);
        await EnsureZoneAsync(db, bSecond.Id, "YB", "Yoğun Bakım", isCritical: false, cancellationToken);
        var operating = await EnsureZoneAsync(db, cFirst.Id, "AML", "Ameliyathane Bölgesi", isCritical: true, cancellationToken);

        await EnsureDoorAsync(db, operating.Id, controller.Id, "AML-GIRIS", "Ameliyathane Giriş", 1, "Giriş Okuyucu 1", hasExit: true, "Çıkış Okuyucu 1", cancellationToken);
        await EnsureDoorAsync(db, operating.Id, controller.Id, "AML-1", "Ameliyathane 1", 2, "Giriş Okuyucu 2", hasExit: false, null, cancellationToken);
        await EnsureDoorAsync(db, operating.Id, controller.Id, "AML-2", "Ameliyathane 2", 3, "Giriş Okuyucu 3", hasExit: false, null, cancellationToken);
        await EnsureDoorAsync(db, operating.Id, controller.Id, "STERIL", "Steril Alan", 4, "Giriş Okuyucu 4", hasExit: false, null, cancellationToken);
    }

    private static async Task<Building> EnsureBuildingAsync(
        UnAccessDbContext db,
        string code,
        string name,
        CancellationToken cancellationToken)
    {
        var existing = await db.Buildings.FirstOrDefaultAsync(b => b.Code == code, cancellationToken);
        if (existing != null)
            return existing;

        var building = new Building { Code = code, Name = name, IsActive = true };
        db.Buildings.Add(building);
        await db.SaveChangesAsync(cancellationToken);
        return building;
    }

    private static async Task<Floor> EnsureFloorAsync(
        UnAccessDbContext db,
        int buildingId,
        string code,
        string name,
        int floorNumber,
        CancellationToken cancellationToken)
    {
        var existing = await db.Floors.FirstOrDefaultAsync(
            f => f.BuildingId == buildingId && f.Code == code,
            cancellationToken);
        if (existing != null)
            return existing;

        var floor = new Floor
        {
            BuildingId = buildingId,
            Code = code,
            Name = name,
            FloorNumber = floorNumber,
            IsActive = true
        };
        db.Floors.Add(floor);
        await db.SaveChangesAsync(cancellationToken);
        return floor;
    }

    private static async Task<Zone> EnsureZoneAsync(
        UnAccessDbContext db,
        int floorId,
        string code,
        string name,
        bool isCritical,
        CancellationToken cancellationToken)
    {
        var existing = await db.Zones.FirstOrDefaultAsync(z => z.FloorId == floorId && z.Code == code, cancellationToken);
        if (existing != null)
            return existing;

        var zone = new Zone
        {
            FloorId = floorId,
            Code = code,
            Name = name,
            IsCritical = isCritical,
            IsActive = true
        };
        db.Zones.Add(zone);
        await db.SaveChangesAsync(cancellationToken);
        return zone;
    }

    private static async Task EnsureDoorAsync(
        UnAccessDbContext db,
        int zoneId,
        int controllerId,
        string code,
        string name,
        int channel,
        string entryReader,
        bool hasExit,
        string? exitReader,
        CancellationToken cancellationToken)
    {
        if (await db.Doors.AnyAsync(d => d.Code == code, cancellationToken))
            return;

        db.Doors.Add(new Door
        {
            ZoneId = zoneId,
            ControllerId = controllerId,
            Code = code,
            Name = name,
            ChannelNumber = channel,
            EntryReaderName = entryReader,
            HasExitReader = hasExit,
            ExitReaderName = exitReader,
            IsCritical = true,
            IsActive = true
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static readonly (string Code, string Name)[] Departments =
    [
        ("GC", "Genel Cerrahi"),
        ("KRD", "Kardiyoloji"),
        ("YB", "Yoğun Bakım"),
        ("ACL", "Acil Servis"),
        ("ECZ", "Eczane"),
        ("BI", "Bilgi İşlem"),
        ("TEK", "Teknik Servis"),
        ("TMZ", "Temizlik"),
        ("GVN", "Güvenlik")
    ];

    private static readonly (string Code, string Name)[] JobTitles =
    [
        ("UZMDR", "Uzman Doktor"),
        ("DOKTOR", "Doktor"),
        ("HEMSIRE", "Hemşire"),
        ("ECZACI", "Eczacı"),
        ("TEKPER", "Teknik Personel"),
        ("GUVPER", "Güvenlik Personeli"),
        ("TMPER", "Temizlik Personeli")
    ];

    private static readonly SamplePerson[] People =
    [
        new("10021", "Ayşe", "Demir", "GC", "UZMDR", "05000000021", "ayse.demir@ornek.unaccess.local", true,
        [
            new("80001021", CardStatus.Lost, new DateOnly(2024, 1, 1), new DateOnly(2025, 12, 31), "Eski kart"),
            new("80001022", CardStatus.Active, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), null)
        ]),
        new("10022", "Mehmet", "Kaya", "KRD", "DOKTOR", "05000000022", "mehmet.kaya@ornek.unaccess.local", true,
        [
            new("80001023", CardStatus.Active, new DateOnly(2026, 1, 1), null, null)
        ]),
        new("10023", "Zeynep", "Arslan", "YB", "HEMSIRE", "05000000023", "zeynep.arslan@ornek.unaccess.local", true,
        [
            new("80001024", CardStatus.Active, new DateOnly(2026, 1, 1), null, null)
        ]),
        new("10024", "Can", "Öztürk", "ACL", "DOKTOR", "05000000024", "can.ozturk@ornek.unaccess.local", true, []),
        new("10025", "Elif", "Aydın", "ECZ", "ECZACI", "05000000025", "elif.aydin@ornek.unaccess.local", true,
        [
            new("80001025", CardStatus.Cancelled, new DateOnly(2025, 3, 1), new DateOnly(2025, 9, 1), "İptal edilmiş kart")
        ]),
        new("10026", "Burak", "Şimşek", "TEK", "TEKPER", "05000000026", "burak.simsek@ornek.unaccess.local", true, []),
        new("10027", "Selin", "Koç", "GVN", "GUVPER", "05000000027", "selin.koc@ornek.unaccess.local", true,
        [
            new("80001027", CardStatus.Active, new DateOnly(2026, 1, 1), null, null)
        ]),
        new("10028", "Emre", "Aksoy", "TMZ", "TMPER", "05000000028", "emre.aksoy@ornek.unaccess.local", false, [])
    ];

    private sealed record SamplePerson(
        string Number,
        string FirstName,
        string LastName,
        string DepartmentCode,
        string JobCode,
        string Phone,
        string Email,
        bool IsActive,
        SampleCard[] Cards);

    private sealed record SampleCard(
        string Number,
        CardStatus Status,
        DateOnly ValidFrom,
        DateOnly? ValidUntil,
        string? Description);
}
