using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Security;
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
    private readonly IUnAccessEnvironment _environment;

    public DatabaseInitializer(
        IDbContextFactory<UnAccessDbContext> factory,
        IConfiguration configuration,
        ILogger<DatabaseInitializer> logger,
        IUtcClock clock,
        IHospitalClock hospitalClock,
        IUnAccessEnvironment environment)
    {
        _factory = factory;
        _configuration = configuration;
        _logger = logger;
        _clock = clock;
        _hospitalClock = hospitalClock;
        _environment = environment;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        if (_environment.AllowsAutomaticMigration && IsEnabled("Database:AutoMigrate"))
        {
            _logger.LogInformation("Veritabanı şeması kontrol ediliyor.");
            await db.Database.MigrateAsync(cancellationToken);
        }
        else if (!await ReadyWithoutMigrationAsync(db, cancellationToken))
        {
            return;
        }

        await SecurityBootstrap.EnsureAsync(db, _clock.UtcNow, cancellationToken);
        var timeZone = await db.SystemSettings.AsNoTracking()
            .Where(item => item.Key == UnAccess.Core.Security.SettingKeys.TimeZone)
            .Select(item => item.Value)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(timeZone))
            _hospitalClock.ApplyTimeZone(timeZone);

        if (_environment.AllowsDevelopmentSeed && IsEnabled("Seed:Enabled"))
        {
            _logger.LogInformation("Örnek veriler kontrol ediliyor.");
            await DevelopmentSeeder.SeedAsync(db, cancellationToken);
        }
    }

    private async Task<bool> ReadyWithoutMigrationAsync(UnAccessDbContext db, CancellationToken cancellationToken)
    {
        var connected = false;
        try
        {
            connected = await db.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Veritabanına bağlanılamadı. Otomatik migration uygulanmadı. {Detail}", SensitiveDataSanitizer.Clean(exception.ToString()));
            return false;
        }

        if (!connected)
        {
            _logger.LogWarning("Veritabanı hazır değil. Otomatik migration uygulanmadı.");
            return false;
        }

        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pending.Any())
        {
            _logger.LogWarning("Bekleyen migration var. Otomatik migration uygulanmadı.");
            return false;
        }

        return true;
    }

    private bool IsEnabled(string key) =>
        bool.TryParse(_configuration[key], out var enabled) && enabled;
}
