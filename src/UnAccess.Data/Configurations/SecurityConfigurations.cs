using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UnAccess.Core.Common;
using UnAccess.Core.Entities;

namespace UnAccess.Data.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Username).IsRequired().HasMaxLength(FieldLengths.Username);
        builder.Property(x => x.PasswordHash).IsRequired().HasMaxLength(FieldLengths.PasswordHash);
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(FieldLengths.Name);
        builder.HasIndex(x => x.Username).IsUnique().HasDatabaseName("UX_Users_Username");
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(FieldLengths.Code);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(FieldLengths.Name);
        builder.Property(x => x.Description).HasMaxLength(FieldLengths.Description);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_Roles_Code");
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");
        builder.HasKey(x => new { x.UserId, x.RoleId }).HasName("UX_UserRoles_User_Role");
        builder.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("SystemSettings");
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasMaxLength(FieldLengths.SettingKey);
        builder.Property(x => x.Value).IsRequired().HasMaxLength(FieldLengths.SettingValue);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
    }
}

internal sealed class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        builder.ToTable("SecurityEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(FieldLengths.EventType);
        builder.Property(x => x.Username).IsRequired().HasMaxLength(FieldLengths.Username);
        builder.Property(x => x.MachineName).IsRequired().HasMaxLength(FieldLengths.ComputerName);
        builder.Property(x => x.Detail).HasMaxLength(FieldLengths.SecurityDetail);
        builder.HasIndex(x => x.OccurredAtUtc).HasDatabaseName("IX_SecurityEvents_OccurredAtUtc");
        builder.HasIndex(x => new { x.Username, x.OccurredAtUtc }).HasDatabaseName("IX_SecurityEvents_Username_OccurredAtUtc");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
    }
}

internal sealed class ServiceHeartbeatConfiguration : IEntityTypeConfiguration<ServiceHeartbeat>
{
    public void Configure(EntityTypeBuilder<ServiceHeartbeat> builder)
    {
        builder.ToTable("ServiceHeartbeats");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ServiceName).IsRequired().HasMaxLength(FieldLengths.ServiceName);
        builder.Property(x => x.InstanceId).IsRequired().HasMaxLength(FieldLengths.InstanceId);
        builder.Property(x => x.MachineName).IsRequired().HasMaxLength(FieldLengths.ComputerName);
        builder.Property(x => x.Version).IsRequired().HasMaxLength(FieldLengths.Version);
        builder.HasIndex(x => new { x.ServiceName, x.InstanceId }).IsUnique().HasDatabaseName("UX_ServiceHeartbeats_Service_Instance");
    }
}
