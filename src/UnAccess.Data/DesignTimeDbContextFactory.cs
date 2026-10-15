using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UnAccess.Data;

public sealed class UnAccessDbContextFactory : IDesignTimeDbContextFactory<UnAccessDbContext>
{
    public UnAccessDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__UnAccess");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__UnAccess ortam değişkeni yok. Bağlantı dizesi kaynak koda yazılmaz.");
        }

        var options = new DbContextOptionsBuilder<UnAccessDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(UnAccessDbContext).Assembly.GetName().Name))
            .Options;

        return new UnAccessDbContext(options);
    }
}
