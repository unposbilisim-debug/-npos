using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Audit;
using UnAccess.Core.Enums;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Sync;
using UnAccess.Core.Models;
using UnAccess.Data;
using UnAccess.Data.Infrastructure;
using UnAccess.Data.Services;
using UnAccess.Data.Seeding;

namespace UnAccess.Tests;

[CollectionDefinition("sql")]
public sealed class SqlCollection : ICollectionFixture<SqlFixture>
{
}

public sealed class SqlFixture : IAsyncLifetime
{
    public bool Available { get; private set; }
    public string SkipReason { get; private set; } = "SQL Server kullanılamıyor.";
    public ServiceProvider? Provider { get; private set; }

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__UnAccess");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            SkipReason = "ConnectionStrings__UnAccess tanımlı değil.";
            return;
        }

        try
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = "UnAccess_" + Guid.NewGuid().ToString("N")
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:UnAccess"] = builder.ConnectionString,
                    ["Database:AutoMigrate"] = "true",
                    ["Seed:Enabled"] = "true"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
            services.AddUnAccessData(configuration);
            Provider = services.BuildServiceProvider();
            await Provider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
            Available = true;
        }
        catch (Exception exception)
        {
            SkipReason = exception.GetBaseException().Message;
            Available = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (Provider == null)
            return;

        try
        {
            if (Available)
            {
                var factory = Provider.GetRequiredService<IDbContextFactory<UnAccessDbContext>>();
                await using var db = await factory.CreateDbContextAsync();
                await db.Database.EnsureDeletedAsync();
            }
        }
        finally
        {
            await Provider.DisposeAsync();
        }
    }
}

[Collection("sql")]
public class PhaseOneDataTests
{
    private readonly SqlFixture _database;

    public PhaseOneDataTests(SqlFixture database)
    {
        _database = database;
    }

    [SkippableFact]
    public async Task Personnel_can_be_created_updated_and_deactivated_without_delete()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var personnel = Service<IPersonnelService>();
        var departments = await personnel.GetDepartmentsAsync(CancellationToken.None);
        var jobs = await personnel.GetJobTitlesAsync(CancellationToken.None);
        var department = departments.Single(item => item.Code == "GC");
        var job = jobs.Single(item => item.Code == "DOKTOR");
        var number = "T" + Guid.NewGuid().ToString("N")[..8];

        var created = await personnel.CreateAsync(new PersonnelEditModel
        {
            PersonnelNumber = number,
            FirstName = "Test",
            LastName = "Personel",
            DepartmentId = department.Id,
            JobTitleId = job.Id,
            Phone = "05000000999",
            Email = "test.personel@ornek.unaccess.local",
            IsActive = true
        }, CancellationToken.None);

        var updated = await personnel.UpdateAsync(created.Id, new PersonnelEditModel
        {
            PersonnelNumber = number,
            FirstName = "Test",
            LastName = "Güncel",
            DepartmentId = department.Id,
            JobTitleId = job.Id,
            IsActive = false
        }, CancellationToken.None);

        Assert.Equal("Güncel", updated.LastName);
        Assert.False(updated.IsActive);
        var loaded = await personnel.GetAsync(created.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.False(loaded.IsActive);

        await using var db = await Factory().CreateDbContextAsync();
        var audits = await db.AuditLogs.Where(log => log.EntityType == AuditEntities.Personnel && log.EntityId == created.Id.ToString()).ToListAsync();
        Assert.Contains(audits, log => log.Action == AuditActions.Created);
        Assert.Contains(audits, log => log.Action == AuditActions.Deactivated);
        Assert.All(audits, log => Assert.False(string.IsNullOrWhiteSpace(log.ComputerName)));
        Assert.All(audits, log => Assert.Equal(DateTimeKind.Utc, log.CreatedAt.Kind));

        var tracked = await db.Personnel.SingleAsync(item => item.Id == created.Id);
        db.Personnel.Remove(tracked);
        await Assert.ThrowsAsync<UnAccessException>(() => db.SaveChangesAsync());
    }

    [SkippableFact]
    public async Task Lost_or_cancelled_card_keeps_personnel_and_history()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var personnel = Service<IPersonnelService>();
        var cards = Service<ICardService>();
        var departments = await personnel.GetDepartmentsAsync(CancellationToken.None);
        var jobs = await personnel.GetJobTitlesAsync(CancellationToken.None);
        var number = "C" + Guid.NewGuid().ToString("N")[..8];
        var person = await personnel.CreateAsync(new PersonnelEditModel
        {
            PersonnelNumber = number,
            FirstName = "Kart",
            LastName = "Test",
            DepartmentId = departments.Single(item => item.Code == "KRD").Id,
            JobTitleId = jobs.Single(item => item.Code == "HEMSIRE").Id,
            IsActive = true
        }, CancellationToken.None);

        var firstNumber = "K" + Guid.NewGuid().ToString("N")[..10];
        var secondNumber = "K" + Guid.NewGuid().ToString("N")[..10];
        var first = await cards.CreateAsync(new CardEditModel
        {
            PersonnelId = person.Id,
            CardNumber = firstNumber,
            ValidFrom = new DateOnly(2026, 1, 1),
            ValidUntil = new DateOnly(2026, 6, 1)
        }, CancellationToken.None);
        var lost = await cards.SetStatusAsync(first.Id, CardStatus.Lost, CancellationToken.None);
        var second = await cards.CreateAsync(new CardEditModel
        {
            PersonnelId = person.Id,
            CardNumber = secondNumber,
            ValidFrom = new DateOnly(2026, 6, 2)
        }, CancellationToken.None);
        var cancelled = await cards.SetStatusAsync(second.Id, CardStatus.Cancelled, CancellationToken.None);

        Assert.Equal(CardStatus.Lost, lost.Status);
        Assert.Equal(CardStatus.Cancelled, cancelled.Status);
        var detail = await personnel.GetAsync(person.Id, CancellationToken.None);
        Assert.NotNull(detail);
        Assert.True(detail.IsActive);
        Assert.Equal(2, detail.Cards.Count);
        Assert.Contains(detail.Cards, card => card.CardNumber == firstNumber && card.Status == CardStatus.Lost);
        Assert.Contains(detail.Cards, card => card.CardNumber == secondNumber && card.Status == CardStatus.Cancelled);

        var listed = await personnel.SearchAsync(new PersonnelSearchCriteria { Text = number }, CancellationToken.None);
        Assert.Equal("", Assert.Single(listed).ActiveCardNumber);

        await personnel.UpdateAsync(person.Id, new PersonnelEditModel
        {
            PersonnelNumber = number,
            FirstName = person.FirstName,
            LastName = person.LastName,
            DepartmentId = person.DepartmentId,
            JobTitleId = person.JobTitleId,
            IsActive = false
        }, CancellationToken.None);
        await Assert.ThrowsAsync<UnAccessException>(() => cards.CreateAsync(new CardEditModel
        {
            PersonnelId = person.Id,
            CardNumber = "K" + Guid.NewGuid().ToString("N")[..10],
            ValidFrom = new DateOnly(2026, 7, 1)
        }, CancellationToken.None));
    }

    [SkippableFact]
    public async Task Seed_builds_hospital_hierarchy_and_simulator_controller()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var hospital = Service<IHospitalStructureService>();
        var controllers = Service<IControllerQueryService>();
        await using var db = await Factory().CreateDbContextAsync();
        await DevelopmentSeeder.SeedAsync(db, CancellationToken.None);

        var doors = await hospital.ListDoorsAsync(null, CancellationToken.None);
        var operatingDoors = doors.Where(door => door.Location == "C BLOK / 1. Kat / Ameliyathane Bölgesi").Select(door => door.Name).ToHashSet();
        Assert.Equal(
            new[] { "Ameliyathane Giriş", "Ameliyathane 1", "Ameliyathane 2", "Steril Alan" }.ToHashSet(),
            operatingDoors);
        Assert.All(doors.Where(door => operatingDoors.Contains(door.Name)), door =>
        {
            Assert.Equal("Test Kontrolörü", door.ControllerName);
            Assert.True(door.IsCritical);
        });

        var zones = await hospital.ListZonesAsync(null, null, CancellationToken.None);
        Assert.Contains(zones, zone => zone.Location == "A BLOK / Zemin Kat" && zone.Name == "Ana Giriş Bölgesi");
        Assert.Contains(zones, zone => zone.Location == "A BLOK / Zemin Kat" && zone.Name == "Acil Servis");
        Assert.Contains(zones, zone => zone.Location == "A BLOK / 1. Kat" && zone.Name == "Poliklinikler");
        Assert.Contains(zones, zone => zone.Location == "B BLOK / 2. Kat" && zone.Name == "Yoğun Bakım");
        Assert.Contains(zones, zone => zone.Location == "D BLOK / Zemin Kat" && zone.Name == "Eczane");
        Assert.Contains(zones, zone => zone.Location == "D BLOK / 1. Kat" && zone.Name == "Bilgi İşlem");
        Assert.Contains(zones, zone => zone.Location == "E BLOK / Zemin Kat" && zone.Name == "Teknik Alan");
        Assert.Equal(
            new[] { "Ana Giriş", "Personel Girişi" }.ToHashSet(),
            NamesAt(doors, "A BLOK / Zemin Kat / Ana Giriş Bölgesi"));
        Assert.Equal(
            new[] { "Acil Servis Personel Girişi", "Acil İlaç Odası" }.ToHashSet(),
            NamesAt(doors, "A BLOK / Zemin Kat / Acil Servis"));
        Assert.Equal(
            new[] { "Poliklinik Personel Girişi" }.ToHashSet(),
            NamesAt(doors, "A BLOK / 1. Kat / Poliklinikler"));
        Assert.Equal(
            new[] { "Yoğun Bakım Giriş", "Yoğun Bakım İlaç Odası" }.ToHashSet(),
            NamesAt(doors, "B BLOK / 2. Kat / Yoğun Bakım"));
        Assert.Equal(
            new[] { "Eczane Giriş", "İlaç Deposu" }.ToHashSet(),
            NamesAt(doors, "D BLOK / Zemin Kat / Eczane"));
        Assert.Equal(
            new[] { "Bilgi İşlem Giriş", "Server Odası" }.ToHashSet(),
            NamesAt(doors, "D BLOK / 1. Kat / Bilgi İşlem"));
        Assert.Equal(
            new[] { "Teknik Servis", "Elektrik Odası" }.ToHashSet(),
            NamesAt(doors, "E BLOK / Zemin Kat / Teknik Alan"));

        var controller = Assert.Single(await controllers.ListAsync(CancellationToken.None), item => item.Name == "Test Kontrolörü");
        Assert.Equal("Simulator", controller.Model);
        Assert.Equal("127.0.0.1", controller.IpAddress);
        Assert.Equal(15050, controller.Port);
        Assert.Null(controller.LastSeenAtUtc);

        var people = await Service<IPersonnelService>().SearchAsync(new PersonnelSearchCriteria { Text = "10021" }, CancellationToken.None);
        var ayse = Assert.Single(people);
        Assert.Equal("80001022", ayse.ActiveCardNumber);
        var detail = await Service<IPersonnelService>().GetAsync(ayse.Id, CancellationToken.None);
        Assert.NotNull(detail);
        Assert.Contains(detail.Cards, card => card.CardNumber == "80001021" && card.Status == CardStatus.Lost);
        Assert.Equal(9, (await Service<IPersonnelService>().GetDepartmentsAsync(CancellationToken.None)).Count);
    }

    [SkippableFact]
    public async Task Second_active_card_is_rejected_until_the_current_one_is_not_active()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var personnel = Service<IPersonnelService>();
        var cards = Service<ICardService>();
        var person = await CreatePersonAsync(personnel, "A");

        var first = await cards.CreateAsync(Card(person.Id, "A1"), CancellationToken.None);
        var duplicate = await Assert.ThrowsAsync<UnAccessException>(() =>
            cards.CreateAsync(Card(person.Id, "A2"), CancellationToken.None));
        Assert.Equal("Bu personelin zaten aktif bir kartı var.", duplicate.UserMessage);

        await cards.SetStatusAsync(first.Id, CardStatus.Lost, CancellationToken.None);
        var second = await cards.CreateAsync(Card(person.Id, "A2"), CancellationToken.None);
        var reactivate = await Assert.ThrowsAsync<UnAccessException>(() =>
            cards.SetStatusAsync(first.Id, CardStatus.Active, CancellationToken.None));
        Assert.Equal("Bu personelin zaten aktif bir kartı var.", reactivate.UserMessage);

        await cards.SetStatusAsync(second.Id, CardStatus.Cancelled, CancellationToken.None);
        var third = await cards.CreateAsync(Card(person.Id, "A3"), CancellationToken.None);

        var detail = await personnel.GetAsync(person.Id, CancellationToken.None);
        Assert.NotNull(detail);
        Assert.Equal(3, detail.Cards.Count);
        Assert.Equal(CardStatus.Lost, detail.Cards.Single(card => card.Id == first.Id).Status);
        Assert.Equal(CardStatus.Cancelled, detail.Cards.Single(card => card.Id == second.Id).Status);
        Assert.Equal(CardStatus.Active, detail.Cards.Single(card => card.Id == third.Id).Status);
    }

    [SkippableFact]
    public async Task Deactivation_cancels_active_cards_and_writes_an_audit_for_each()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var personnel = Service<IPersonnelService>();
        var cards = Service<ICardService>();
        var person = await CreatePersonAsync(personnel, "P");
        var lost = await cards.CreateAsync(Card(person.Id, "P1"), CancellationToken.None);
        await cards.SetStatusAsync(lost.Id, CardStatus.Lost, CancellationToken.None);
        var active = await cards.CreateAsync(Card(person.Id, "P2"), CancellationToken.None);

        var updated = await personnel.UpdateAsync(person.Id, Edit(person, isActive: false), CancellationToken.None);

        Assert.False(updated.IsActive);
        Assert.Equal(2, updated.Cards.Count);
        Assert.Equal(CardStatus.Cancelled, updated.Cards.Single(card => card.Id == active.Id).Status);
        Assert.Equal(CardStatus.Lost, updated.Cards.Single(card => card.Id == lost.Id).Status);

        await using var db = await Factory().CreateDbContextAsync();
        var storedCards = await db.Cards.Where(card => card.PersonnelId == person.Id).ToListAsync();
        Assert.Equal(2, storedCards.Count);
        Assert.Contains(storedCards, card => card.Id == active.Id && card.Status == CardStatus.Cancelled);
        Assert.Contains(storedCards, card => card.Id == lost.Id && card.Status == CardStatus.Lost);

        var personnelAudits = await db.AuditLogs
            .Where(log => log.EntityType == AuditEntities.Personnel && log.EntityId == person.Id.ToString())
            .ToListAsync();
        Assert.Contains(personnelAudits, log => log.Action == AuditActions.Deactivated);

        var cardAudits = await db.AuditLogs
            .Where(log => log.EntityType == AuditEntities.Card && log.EntityId == active.Id.ToString())
            .ToListAsync();
        Assert.Contains(cardAudits, log =>
            log.Action == AuditActions.StatusChanged &&
            log.Description.Contains(active.CardNumber, StringComparison.Ordinal));
        Assert.DoesNotContain(
            await db.AuditLogs.Where(log => log.EntityId == lost.Id.ToString()).Select(log => log.Description).ToListAsync(),
            description => description.Contains("pasife alındığı için", StringComparison.Ordinal));
    }

    [SkippableFact]
    public async Task Failed_deactivation_rolls_back_personnel_and_active_cards_together()
    {
        Skip.IfNot(_database.Available, _database.SkipReason);
        var personnel = Service<IPersonnelService>();
        var cards = Service<ICardService>();
        var person = await CreatePersonAsync(personnel, "R");
        var active = await cards.CreateAsync(Card(person.Id, "R1"), CancellationToken.None);

        await using var probe = await Factory().CreateDbContextAsync();
        var connectionString = probe.Database.GetConnectionString();
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = new DbContextOptionsBuilder<UnAccessDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(new FailOnSecondSaveInterceptor())
            .Options;
        var service = new PersonnelService(
            new FixedContextFactory(options),
            new AuditWriter(),
            NullLogger<PersonnelService>.Instance,
            new DeviceSyncOutbox(new SystemUtcClock(), new DeviceSyncOptions()));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.UpdateAsync(person.Id, Edit(person, isActive: false), CancellationToken.None));

        await using var db = await Factory().CreateDbContextAsync();
        var storedPerson = await db.Personnel.SingleAsync(item => item.Id == person.Id);
        var storedCard = await db.Cards.SingleAsync(card => card.Id == active.Id);
        Assert.True(storedPerson.IsActive);
        Assert.Equal(CardStatus.Active, storedCard.Status);
        Assert.DoesNotContain(
            await db.AuditLogs.Where(log => log.EntityId == person.Id.ToString()).Select(log => log.Action).ToListAsync(),
            action => action == AuditActions.Deactivated);
        Assert.DoesNotContain(
            await db.AuditLogs.Where(log => log.EntityId == active.Id.ToString()).Select(log => log.Description).ToListAsync(),
            description => description.Contains("pasife alındığı için", StringComparison.Ordinal));
    }

    private async Task<PersonnelDetail> CreatePersonAsync(IPersonnelService personnel, string prefix)
    {
        var departments = await personnel.GetDepartmentsAsync(CancellationToken.None);
        var jobs = await personnel.GetJobTitlesAsync(CancellationToken.None);
        return await personnel.CreateAsync(new PersonnelEditModel
        {
            PersonnelNumber = prefix + Guid.NewGuid().ToString("N")[..8],
            FirstName = "Kural",
            LastName = "Test",
            DepartmentId = departments.Single(item => item.Code == "GC").Id,
            JobTitleId = jobs.Single(item => item.Code == "DOKTOR").Id,
            IsActive = true
        }, CancellationToken.None);
    }

    private static PersonnelEditModel Edit(PersonnelDetail person, bool isActive) => new()
    {
        PersonnelNumber = person.PersonnelNumber,
        FirstName = person.FirstName,
        LastName = person.LastName,
        DepartmentId = person.DepartmentId,
        JobTitleId = person.JobTitleId,
        Phone = person.Phone,
        Email = person.Email,
        PhotoPath = person.PhotoPath,
        IsActive = isActive
    };

    private static CardEditModel Card(int personnelId, string suffix) => new()
    {
        PersonnelId = personnelId,
        CardNumber = suffix + Guid.NewGuid().ToString("N")[..10],
        ValidFrom = new DateOnly(2026, 1, 1)
    };

    private static HashSet<string> NamesAt(IEnumerable<DoorListItem> doors, string location) =>
        doors.Where(door => door.Location == location).Select(door => door.Name).ToHashSet();

    private T Service<T>() where T : notnull
    {
        return _database.Provider!.GetRequiredService<T>();
    }

    private IDbContextFactory<UnAccessDbContext> Factory()
    {
        return _database.Provider!.GetRequiredService<IDbContextFactory<UnAccessDbContext>>();
    }

    private sealed class FixedContextFactory : IDbContextFactory<UnAccessDbContext>
    {
        private readonly DbContextOptions<UnAccessDbContext> _options;

        public FixedContextFactory(DbContextOptions<UnAccessDbContext> options)
        {
            _options = options;
        }

        public UnAccessDbContext CreateDbContext() => new(_options);

        public Task<UnAccessDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class FailOnSecondSaveInterceptor : SaveChangesInterceptor
    {
        private int _saves;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            _saves++;
            if (_saves >= 2)
                throw new InvalidOperationException("Denetim kaydı yazılamadı.");

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
