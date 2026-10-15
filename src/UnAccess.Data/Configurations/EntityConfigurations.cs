using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UnAccess.Core.Common;
using UnAccess.Core.Entities;

namespace UnAccess.Data.Configurations;

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(FieldLengths.Code);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(FieldLengths.Name);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_Departments_Code");
    }
}

internal sealed class JobTitleConfiguration : IEntityTypeConfiguration<JobTitle>
{
    public void Configure(EntityTypeBuilder<JobTitle> builder)
    {
        builder.ToTable("JobTitles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(FieldLengths.Code);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(FieldLengths.Name);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_JobTitles_Code");
    }
}

internal sealed class PersonnelConfiguration : IEntityTypeConfiguration<Personnel>
{
    public void Configure(EntityTypeBuilder<Personnel> builder)
    {
        builder.ToTable("Personnel");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PersonnelNumber).IsRequired().HasMaxLength(FieldLengths.PersonnelNumber);
        builder.Property(x => x.FirstName).IsRequired().HasMaxLength(FieldLengths.PersonName);
        builder.Property(x => x.LastName).IsRequired().HasMaxLength(FieldLengths.PersonName);
        builder.Property(x => x.Phone).HasMaxLength(FieldLengths.Phone);
        builder.Property(x => x.Email).HasMaxLength(FieldLengths.Email);
        builder.Property(x => x.PhotoPath).HasMaxLength(FieldLengths.PhotoPath);
        builder.HasIndex(x => x.PersonnelNumber).IsUnique().HasDatabaseName("UX_Personnel_PersonnelNumber");
        builder.HasIndex(x => x.DepartmentId).HasDatabaseName("IX_Personnel_DepartmentId");
        builder.HasIndex(x => x.JobTitleId).HasDatabaseName("IX_Personnel_JobTitleId");
        builder.HasOne(x => x.Department)
            .WithMany()
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
        builder.HasOne(x => x.JobTitle)
            .WithMany()
            .HasForeignKey(x => x.JobTitleId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}

internal sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.ToTable("Cards");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CardNumber).IsRequired().HasMaxLength(FieldLengths.CardNumber);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(FieldLengths.Description);
        builder.HasIndex(x => x.CardNumber).IsUnique().HasDatabaseName("UX_Cards_CardNumber");
        builder.HasIndex(x => x.PersonnelId).HasDatabaseName("IX_Cards_PersonnelId");
        builder.HasOne(x => x.Personnel)
            .WithMany()
            .HasForeignKey(x => x.PersonnelId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}

internal sealed class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.ToTable("Buildings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(FieldLengths.Code);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(FieldLengths.Name);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_Buildings_Code");
    }
}

internal sealed class FloorConfiguration : IEntityTypeConfiguration<Floor>
{
    public void Configure(EntityTypeBuilder<Floor> builder)
    {
        builder.ToTable("Floors");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(FieldLengths.Code);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(FieldLengths.Name);
        builder.HasIndex(x => new { x.BuildingId, x.Code }).IsUnique().HasDatabaseName("UX_Floors_Building_Code");
        builder.HasIndex(x => new { x.BuildingId, x.FloorNumber }).IsUnique().HasDatabaseName("UX_Floors_Building_Number");
        builder.HasOne(x => x.Building)
            .WithMany()
            .HasForeignKey(x => x.BuildingId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}

internal sealed class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> builder)
    {
        builder.ToTable("Zones");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(FieldLengths.Code);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(FieldLengths.Name);
        builder.HasIndex(x => new { x.FloorId, x.Code }).IsUnique().HasDatabaseName("UX_Zones_Floor_Code");
        builder.HasOne(x => x.Floor)
            .WithMany()
            .HasForeignKey(x => x.FloorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}

internal sealed class DoorConfiguration : IEntityTypeConfiguration<Door>
{
    public void Configure(EntityTypeBuilder<Door> builder)
    {
        builder.ToTable("Doors");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(FieldLengths.Code);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(FieldLengths.Name);
        builder.Property(x => x.EntryReaderName).HasMaxLength(FieldLengths.ReaderName);
        builder.Property(x => x.ExitReaderName).HasMaxLength(FieldLengths.ReaderName);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_Doors_Code");
        builder.HasIndex(x => x.ZoneId).HasDatabaseName("IX_Doors_ZoneId");
        builder.HasIndex(x => x.ControllerId).HasDatabaseName("IX_Doors_ControllerId");
        builder.HasOne(x => x.Zone)
            .WithMany()
            .HasForeignKey(x => x.ZoneId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
        builder.HasOne(x => x.Controller)
            .WithMany()
            .HasForeignKey(x => x.ControllerId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}

internal sealed class ControllerConfiguration : IEntityTypeConfiguration<Controller>
{
    public void Configure(EntityTypeBuilder<Controller> builder)
    {
        builder.ToTable("Controllers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(FieldLengths.Code);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(FieldLengths.Name);
        builder.Property(x => x.Brand).IsRequired().HasMaxLength(FieldLengths.Brand);
        builder.Property(x => x.Model).IsRequired().HasMaxLength(FieldLengths.Model);
        builder.Property(x => x.IpAddress).IsRequired().HasMaxLength(FieldLengths.IpAddress);
        builder.Property(x => x.SerialNumber).HasMaxLength(FieldLengths.SerialNumber);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_Controllers_Code");
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(FieldLengths.AuditAction);
        builder.Property(x => x.EntityType).IsRequired().HasMaxLength(FieldLengths.AuditEntityType);
        builder.Property(x => x.EntityId).HasMaxLength(FieldLengths.AuditEntityId);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(FieldLengths.AuditDescription);
        builder.Property(x => x.OldValue).HasMaxLength(FieldLengths.AuditValue);
        builder.Property(x => x.NewValue).HasMaxLength(FieldLengths.AuditValue);
        builder.Property(x => x.ComputerName).IsRequired().HasMaxLength(FieldLengths.ComputerName);
        builder.HasIndex(x => new { x.EntityType, x.EntityId }).HasDatabaseName("IX_AuditLogs_Entity");
        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_AuditLogs_CreatedAt");
    }
}
