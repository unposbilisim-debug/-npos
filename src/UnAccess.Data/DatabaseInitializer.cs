using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Data.Infrastructure;
using UnAccess.Data.Seeding;

namespace UnAccess.Data;

public sealed class DatabaseInitializer
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseInitializer> _logger;
    private readonly IUtcClock _clock;
    private readonly IHospitalClock _hospitalClock;

    public DatabaseInitializer(
        IDbContextFactory<UnAccessDbContext> factory,
        IConfiguration configuration,
        ILogger<DatabaseInitializer> logger,
        IUtcClock clock,
        IHospitalClock hospitalClock)
    {
        _factory = factory;
        _configuration = configuration;
        _logger = logger;
        _clock = clock;
        _hospitalClock = hospitalClock;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        if (IsEnabled("Database:AutoMigrate"))
        {
            _logger.LogInformation("Veritabanı şeması kontrol ediliyor.");
            await db.Database.MigrateAsync(cancellationToken);
        }

        await SecurityBootstrap.EnsureAsync(db, _clock.UtcNow, cancellationToken);
        var timeZone = await db.SystemSettings.AsNoTracking()
            .Where(item => item.Key == UnAccess.Core.Security.SettingKeys.TimeZone)
            .Select(item => item.Value)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(timeZone))
            _hospitalClock.ApplyTimeZone(timeZone);

        if (IsEnabled("Seed:Enabled"))
        {
            _logger.LogInformation("Örnek veriler kontrol ediliyor.");
            await DevelopmentSeeder.SeedAsync(db, cancellationToken);
        }
    }

    private bool IsEnabled(string key) =>
        bool.TryParse(_configuration[key], out var enabled) && enabled;
}
