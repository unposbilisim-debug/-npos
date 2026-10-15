using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Audit;
using UnAccess.Core.Enums;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Data;
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
        Assert.Contains(zones, zone => zone.Location == "A BLOK / Zemin Kat" && zone.Name == "Ana Giriş");
        Assert.Contains(zones, zone => zone.Location == "A BLOK / Zemin Kat" && zone.Name == "Acil Servis");
        Assert.Contains(zones, zone => zone.Location == "A BLOK / 1. Kat" && zone.Name == "Poliklinikler");
        Assert.Contains(zones, zone => zone.Location == "B BLOK / 2. Kat" && zone.Name == "Yoğun Bakım");

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

    private T Service<T>() where T : notnull
    {
        return _database.Provider!.GetRequiredService<T>();
    }

    private IDbContextFactory<UnAccessDbContext> Factory()
    {
        return _database.Provider!.GetRequiredService<IDbContextFactory<UnAccessDbContext>>();
    }
}
