using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using UnAccess.Core.Abstractions;

namespace UnAccess.Data.Services;

internal sealed class DatabaseHealthService : IDatabaseHealthService
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;

    public DatabaseHealthService(IDbContextFactory<UnAccessDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<DatabaseHealthReport> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var connected = await db.Database.CanConnectAsync(cancellationToken);
            if (!connected)
            {
                return new DatabaseHealthReport
                {
                    Connected = false,
                    ConnectionText = "Bağlantı yok",
                    PendingMigrations = false,
                    MigrationText = "Bilinmiyor",
                    ModelText = "Bilinmiyor",
                    TablesReadable = false
                };
            }

            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any();
            var tables = await TablesReadableAsync(db, cancellationToken);
            return new DatabaseHealthReport
            {
                Connected = true,
                ConnectionText = "Bağlı",
                PendingMigrations = pending,
                MigrationText = pending ? "Bekleyen migration var" : "Güncel",
                ModelText = ModelText(db),
                TablesReadable = tables && !pending
            };
        }
        catch (Exception)
        {
            return new DatabaseHealthReport
            {
                Connected = false,
                ConnectionText = "Hata",
                PendingMigrations = false,
                MigrationText = "Bilinmiyor",
                ModelText = "Bilinmiyor",
                TablesReadable = false
            };
        }
    }

    private static async Task<bool> TablesReadableAsync(UnAccessDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            _ = await db.SystemSettings.AsNoTracking().Select(item => item.Key).Take(1).ToListAsync(cancellationToken);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string ModelText(UnAccessDbContext db)
    {
        try
        {
            var differ = db.GetService<IMigrationsModelDiffer>();
            var migrations = db.GetService<IMigrationsAssembly>();
            var snapshot = migrations.ModelSnapshot?.Model;
            if (differ == null || snapshot == null)
                return "Bilinmiyor";

            var current = db.GetService<IDesignTimeModel>()?.Model;
            if (current == null)
                return "Bilinmiyor";

            var different = differ.HasDifferences(snapshot.GetRelationalModel(), current.GetRelationalModel());
            return different ? "Model farkı var" : "Model uyumlu";
        }
        catch (Exception)
        {
            return "Bilinmiyor";
        }
    }
}
