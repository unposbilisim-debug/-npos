using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using UnAccess.Core.Entities;
using UnAccess.Data;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Projects_keep_ui_database_and_devices_apart()
    {
        var root = Repo.Root();
        var core = File.ReadAllText(Path.Combine(root, "src", "UnAccess.Core", "UnAccess.Core.csproj"));
        var devices = File.ReadAllText(Path.Combine(root, "src", "UnAccess.Devices", "UnAccess.Devices.csproj"));
        var simulator = File.ReadAllText(Path.Combine(root, "src", "UnAccess.Simulator", "UnAccess.Simulator.csproj"));
        var data = File.ReadAllText(Path.Combine(root, "src", "UnAccess.Data", "UnAccess.Data.csproj"));

        Assert.DoesNotContain("ProjectReference", core, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ProjectReference", devices, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UnAccess.Devices", simulator, StringComparison.Ordinal);
        Assert.DoesNotContain("UnAccess.Data", simulator, StringComparison.Ordinal);
        Assert.DoesNotContain("UnAccess.Core", simulator, StringComparison.Ordinal);
        Assert.Contains("UnAccess.Core", data, StringComparison.Ordinal);
        Assert.DoesNotContain("UnAccess.Devices", data, StringComparison.Ordinal);
        Assert.DoesNotContain("UnAccess.Simulator", data, StringComparison.Ordinal);
    }

    [Fact]
    public void Source_does_not_embed_connection_strings_or_call_devices_from_forms()
    {
        var root = Repo.Root();
        var sourceFiles = Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories);
        foreach (var file in sourceFiles)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("Password=", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Pwd=", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Server=", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("NetSDK", text, StringComparison.OrdinalIgnoreCase);
        }

        var formFiles = Directory.GetFiles(Path.Combine(root, "src", "UnAccess.Desktop", "Forms"), "*.cs");
        foreach (var file in formFiles)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("DbContext", text, StringComparison.Ordinal);
            Assert.DoesNotContain("SqlCommand", text, StringComparison.Ordinal);
            Assert.DoesNotContain("ExecuteSql", text, StringComparison.Ordinal);
            Assert.DoesNotContain("DahuaSimulator", text, StringComparison.Ordinal);
            Assert.DoesNotContain("IAccessController", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Initial_migration_defines_the_phase_one_tables()
    {
        var root = Repo.Root();
        var snapshot = File.ReadAllText(Path.Combine(
            root,
            "src",
            "UnAccess.Data",
            "Migrations",
            "UnAccessDbContextModelSnapshot.cs"));

        foreach (var table in new[]
        {
            "Departments", "JobTitles", "Personnel", "Cards", "Buildings",
            "Floors", "Zones", "Doors", "Controllers", "AuditLogs"
        })
        {
            Assert.Contains($"\"{table}\"", snapshot, StringComparison.Ordinal);
        }

        Assert.Contains("UX_Personnel_PersonnelNumber", snapshot, StringComparison.Ordinal);
        Assert.Contains("UX_Cards_CardNumber", snapshot, StringComparison.Ordinal);
        Assert.Contains("DeleteBehavior.Restrict", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Model_matches_the_migration_snapshot()
    {
        var options = new DbContextOptionsBuilder<UnAccessDbContext>()
            .UseSqlServer("Data Source=localhost;Initial Catalog=UnAccessDB;Integrated Security=True;TrustServerCertificate=True")
            .Options;
        using var db = new UnAccessDbContext(options);
        var snapshot = new UnAccess.Data.Migrations.UnAccessDbContextModelSnapshot();
        var initializer = db.GetService<IModelRuntimeInitializer>();
        var snapshotModel = initializer.Initialize(snapshot.Model);
        var differ = db.GetService<IMigrationsModelDiffer>();
        var differences = differ.GetDifferences(
            snapshotModel.GetRelationalModel(),
            db.GetService<IDesignTimeModel>().Model.GetRelationalModel()).ToList();
        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences.Select(difference => difference.ToString())));
    }

    [Fact]
    public void Materialized_timestamps_are_marked_utc()
    {
        var options = new DbContextOptionsBuilder<UnAccessDbContext>()
            .UseSqlServer("Data Source=localhost;Initial Catalog=UnAccessDB;Integrated Security=True;TrustServerCertificate=True")
            .Options;
        using var db = new UnAccessDbContext(options);
        var department = new Department
        {
            CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Unspecified),
            UpdatedAt = new DateTime(2026, 1, 2, 3, 4, 6, DateTimeKind.Local)
        };
        UtcMaterializationInterceptor.Apply(department, db.Model.FindEntityType(typeof(Department))!);
        Assert.Equal(DateTimeKind.Utc, department.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, department.UpdatedAt.Kind);
        Assert.Equal(3, department.CreatedAt.Hour);
    }
}

internal static class Repo
{
    public static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "UnAccess.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Solution kökü bulunamadı.");
    }
}
