using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UnAccess.Core.Common;
using UnAccess.Core.Entities;

namespace UnAccess.Data.Configurations;

internal sealed class AccessGroupConfiguration : IEntityTypeConfiguration<AccessGroup>
{
    public void Configure(EntityTypeBuilder<AccessGroup> builder)
    {
        builder.ToTable("AccessGroups");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(FieldLengths.Code);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(FieldLengths.Name);
        builder.Property(x => x.Description).HasMaxLength(FieldLengths.Description);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_AccessGroups_Code");
    }
}

internal sealed class PersonnelAccessGroupConfiguration : IEntityTypeConfiguration<PersonnelAccessGroup>
{
    public void Configure(EntityTypeBuilder<PersonnelAccessGroup> builder)
    {
        builder.ToTable("PersonnelAccessGroups");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.PersonnelId, x.AccessGroupId }).IsUnique().HasDatabaseName("UX_PersonnelAccessGroups_Person_Group");
        builder.HasIndex(x => x.PersonnelId).HasDatabaseName("IX_PersonnelAccessGroups_PersonnelId");
        builder.HasOne(x => x.Personnel).WithMany().HasForeignKey(x => x.PersonnelId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(x => x.AccessGroup).WithMany().HasForeignKey(x => x.AccessGroupId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}

internal sealed class TimeScheduleConfiguration : IEntityTypeConfiguration<TimeSchedule>
{
    public void Configure(EntityTypeBuilder<TimeSchedule> builder)
    {
        builder.ToTable("TimeSchedules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(FieldLengths.Code);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(FieldLengths.Name);
        builder.Property(x => x.Description).HasMaxLength(FieldLengths.Description);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_TimeSchedules_Code");
    }
}

internal sealed class TimeScheduleDetailConfiguration : IEntityTypeConfiguration<TimeScheduleDetail>
{
    public void Configure(EntityTypeBuilder<TimeScheduleDetail> builder)
    {
        builder.ToTable("TimeScheduleDetails");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DayOfWeek).HasConversion<int>();
        builder.HasIndex(x => x.TimeScheduleId).HasDatabaseName("IX_TimeScheduleDetails_TimeScheduleId");
        builder.HasOne(x => x.TimeSchedule).WithMany().HasForeignKey(x => x.TimeScheduleId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}

internal sealed class AccessGroupZoneConfiguration : IEntityTypeConfiguration<AccessGroupZone>
{
    public void Configure(EntityTypeBuilder<AccessGroupZone> builder)
    {
        builder.ToTable("AccessGroupZones");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.AccessGroupId, x.ZoneId }).IsUnique().HasDatabaseName("UX_AccessGroupZones_Group_Zone");
        builder.HasIndex(x => x.ZoneId).HasDatabaseName("IX_AccessGroupZones_ZoneId");
        builder.HasOne(x => x.AccessGroup).WithMany().HasForeignKey(x => x.AccessGroupId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(x => x.TimeSchedule).WithMany().HasForeignKey(x => x.TimeScheduleId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}

internal sealed class AccessGroupDoorConfiguration : IEntityTypeConfiguration<AccessGroupDoor>
{
    public void Configure(EntityTypeBuilder<AccessGroupDoor> builder)
    {
        builder.ToTable("AccessGroupDoors");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.AccessGroupId, x.DoorId }).IsUnique().HasDatabaseName("UX_AccessGroupDoors_Group_Door");
        builder.HasIndex(x => x.DoorId).HasDatabaseName("IX_AccessGroupDoors_DoorId");
        builder.HasOne(x => x.AccessGroup).WithMany().HasForeignKey(x => x.AccessGroupId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(x => x.Door).WithMany().HasForeignKey(x => x.DoorId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(x => x.TimeSchedule).WithMany().HasForeignKey(x => x.TimeScheduleId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}

internal sealed class PersonnelAccessOverrideConfiguration : IEntityTypeConfiguration<PersonnelAccessOverride>
{
    public void Configure(EntityTypeBuilder<PersonnelAccessOverride> builder)
    {
        builder.ToTable("PersonnelAccessOverrides", table => table.HasCheckConstraint(
            "CK_PersonnelAccessOverrides_Target",
            "([ZoneId] IS NOT NULL AND [DoorId] IS NULL) OR ([ZoneId] IS NULL AND [DoorId] IS NOT NULL)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(FieldLengths.Description);
        builder.HasIndex(x => new { x.PersonnelId, x.DoorId }).HasDatabaseName("IX_PersonnelAccessOverrides_Personnel_Door");
        builder.HasIndex(x => new { x.PersonnelId, x.ZoneId }).HasDatabaseName("IX_PersonnelAccessOverrides_Personnel_Zone");
        builder.HasOne(x => x.Personnel).WithMany().HasForeignKey(x => x.PersonnelId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        builder.HasOne(x => x.Door).WithMany().HasForeignKey(x => x.DoorId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        builder.HasOne(x => x.TimeSchedule).WithMany().HasForeignKey(x => x.TimeScheduleId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}

internal sealed class AccessEventConfiguration : IEntityTypeConfiguration<AccessEvent>
{
    public void Configure(EntityTypeBuilder<AccessEvent> builder)
    {
        builder.ToTable("AccessEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventId).IsRequired().HasMaxLength(FieldLengths.EventId);
        builder.Property(x => x.CardNumber).IsRequired().HasMaxLength(FieldLengths.CardNumber);
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(FieldLengths.EventType);
        builder.Property(x => x.Decision).IsRequired().HasMaxLength(FieldLengths.Decision);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(FieldLengths.Reason);
        builder.Property(x => x.Source).IsRequired().HasMaxLength(FieldLengths.EventSource);
        builder.Property(x => x.RawData).HasMaxLength(FieldLengths.RawData);
        builder.HasIndex(x => x.EventId).IsUnique().HasDatabaseName("UX_AccessEvents_EventId");
        builder.HasIndex(x => x.OccurredAtUtc).HasDatabaseName("IX_AccessEvents_OccurredAtUtc");
        builder.HasIndex(x => new { x.DoorId, x.OccurredAtUtc }).HasDatabaseName("IX_AccessEvents_Door_OccurredAtUtc");
        builder.HasIndex(x => new { x.PersonnelId, x.OccurredAtUtc }).HasDatabaseName("IX_AccessEvents_Personnel_OccurredAtUtc");
        builder.HasIndex(x => new { x.CardNumber, x.OccurredAtUtc }).HasDatabaseName("IX_AccessEvents_Card_OccurredAtUtc");
        builder.HasOne(x => x.Controller).WithMany().HasForeignKey(x => x.ControllerId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        builder.HasOne(x => x.Door).WithMany().HasForeignKey(x => x.DoorId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(x => x.Personnel).WithMany().HasForeignKey(x => x.PersonnelId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        builder.HasOne(x => x.Card).WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
    }
}
