using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UnAccess.Core.Common;
using UnAccess.Core.Entities;

namespace UnAccess.Data.Configurations;

internal sealed class BackupHistoryConfiguration : IEntityTypeConfiguration<BackupHistory>
{
    public void Configure(EntityTypeBuilder<BackupHistory> builder)
    {
        builder.ToTable("BackupHistory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BackupType).IsRequired().HasMaxLength(32);
        builder.Property(x => x.FileName).IsRequired().HasMaxLength(FieldLengths.BackupFileName);
        builder.Property(x => x.BackupDirectory).IsRequired().HasMaxLength(FieldLengths.SettingValue);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.TriggeredBy).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ErrorMessage).HasMaxLength(FieldLengths.ErrorMessage);
        builder.HasIndex(x => new { x.BackupType, x.Status, x.CompletedAtUtc }).HasDatabaseName("IX_BackupHistory_Type_Status_Completed");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.TriggeredByUserId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
    }
}

internal sealed class RestoreHistoryConfiguration : IEntityTypeConfiguration<RestoreHistory>
{
    public void Configure(EntityTypeBuilder<RestoreHistory> builder)
    {
        builder.ToTable("RestoreHistory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BackupFileName).IsRequired().HasMaxLength(FieldLengths.BackupFileName);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.SafetyBackupFileName).HasMaxLength(FieldLengths.BackupFileName);
        builder.Property(x => x.ErrorMessage).HasMaxLength(FieldLengths.ErrorMessage);
        builder.HasIndex(x => x.StartedAtUtc).HasDatabaseName("IX_RestoreHistory_StartedAtUtc");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.TriggeredByUserId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
    }
}
