using Microsoft.EntityFrameworkCore;
using UnAccess.Core.Common;
using UnAccess.Core.Entities;
using UnAccess.Core.Exceptions;

namespace UnAccess.Data;

public sealed class UnAccessDbContext : DbContext
{
    public UnAccessDbContext(DbContextOptions<UnAccessDbContext> options)
        : base(options)
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<JobTitle> JobTitles => Set<JobTitle>();
    public DbSet<Personnel> Personnel => Set<Personnel>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Door> Doors => Set<Door>();
    public DbSet<Controller> Controllers => Set<Controller>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AccessGroup> AccessGroups => Set<AccessGroup>();
    public DbSet<PersonnelAccessGroup> PersonnelAccessGroups => Set<PersonnelAccessGroup>();
    public DbSet<TimeSchedule> TimeSchedules => Set<TimeSchedule>();
    public DbSet<TimeScheduleDetail> TimeScheduleDetails => Set<TimeScheduleDetail>();
    public DbSet<AccessGroupZone> AccessGroupZones => Set<AccessGroupZone>();
    public DbSet<AccessGroupDoor> AccessGroupDoors => Set<AccessGroupDoor>();
    public DbSet<PersonnelAccessOverride> PersonnelAccessOverrides => Set<PersonnelAccessOverride>();
    public DbSet<AccessEvent> AccessEvents => Set<AccessEvent>();
    public DbSet<DeviceSyncQueue> DeviceSyncQueue => Set<DeviceSyncQueue>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<ServiceHeartbeat> ServiceHeartbeats => Set<ServiceHeartbeat>();
    public DbSet<BackupHistory> BackupHistories => Set<BackupHistory>();
    public DbSet<RestoreHistory> RestoreHistories => Set<RestoreHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UnAccessDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        PrepareChanges();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PrepareChanges();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void PrepareChanges()
    {
        var utcNow = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Deleted && entry.Entity is not UserRole)
                throw new UnAccessException("Kayıtlar fiziksel olarak silinemez. Kaydı pasife alın veya durumunu değiştirin.");

            if (entry.State == EntityState.Modified && entry.Entity is AuditLog or SecurityEvent)
                throw new UnAccessException("Denetim ve güvenlik kayıtları değiştirilemez.");

            if (entry.Entity is not IHasTimestamps timestamps)
                continue;

            if (entry.State == EntityState.Added)
            {
                timestamps.CreatedAt = utcNow;
                timestamps.UpdatedAt = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                timestamps.UpdatedAt = utcNow;
                entry.Property(nameof(IHasTimestamps.CreatedAt)).IsModified = false;
            }
        }
    }
}
