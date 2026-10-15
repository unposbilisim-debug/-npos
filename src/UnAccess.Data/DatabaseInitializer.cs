using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UnAccess.Data.Seeding;

namespace UnAccess.Data;

public sealed class DatabaseInitializer
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IDbContextFactory<UnAccessDbContext> factory,
        IConfiguration configuration,
        ILogger<DatabaseInitializer> logger)
    {
        _factory = factory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        if (IsEnabled("Database:AutoMigrate"))
        {
            _logger.LogInformation("Veritabanı şeması kontrol ediliyor.");
            await db.Database.MigrateAsync(cancellationToken);
        }

        if (IsEnabled("Seed:Enabled"))
        {
            _logger.LogInformation("Örnek veriler kontrol ediliyor.");
            await DevelopmentSeeder.SeedAsync(db, cancellationToken);
        }
    }

    private bool IsEnabled(string key) =>
        bool.TryParse(_configuration[key], out var enabled) && enabled;
}
