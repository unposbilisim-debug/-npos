using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Backup;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Core.Security;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Tests;

public class OperationsUnitTests
{
    [Fact]
    public void Schedule_runs_at_the_planned_time_and_not_twice()
    {
        var now = new DateTime(2026, 10, 15, 2, 0, 0);
        Assert.True(BackupSchedule.ShouldRun(true, new TimeOnly(2, 0), now, null));
        Assert.False(BackupSchedule.ShouldRun(true, new TimeOnly(2, 0), now, new DateOnly(2026, 10, 15)));
        Assert.False(BackupSchedule.ShouldRun(false, new TimeOnly(2, 0), now, null));
        Assert.False(BackupSchedule.ShouldRun(true, new TimeOnly(3, 0), now, null));
    }

    [Fact]
    public void Missed_schedule_runs_once_after_the_planned_time()
    {
        var now = new DateTime(2026, 10, 15, 14, 30, 0);
        Assert.True(BackupSchedule.ShouldRun(true, new TimeOnly(2, 0), now, null));
        Assert.True(BackupSchedule.ShouldRun(true, new TimeOnly(2, 0), now, new DateOnly(2026, 10, 14)));
        Assert.False(BackupSchedule.ShouldRun(true, new TimeOnly(2, 0), now, new DateOnly(2026, 10, 15)));
    }

    [Fact]
    public void Backup_names_follow_the_required_pattern()
    {
        Assert.Equal("UnAccessDB_20261015_020000.bak", BackupFiles.Create("UnAccessDB", new DateTime(2026, 10, 15, 2, 0, 0), BackupKinds.Scheduled));
        Assert.Equal("UnAccessDB_PreRestore_20261015_143000.bak", BackupFiles.Create("UnAccessDB", new DateTime(2026, 10, 15, 14, 30, 0), BackupKinds.PreRestoreSafety));
        Assert.True(BackupFiles.IsScheduledFile("UnAccessDB_20261015_020000.bak"));
        Assert.False(BackupFiles.IsScheduledFile("UnAccessDB_PreRestore_20261015_143000.bak"));
        Assert.False(BackupFiles.IsScheduledFile("notes.bak"));
    }

    [Fact]
    public void Retention_selects_only_old_completed_scheduled_files()
    {
        var now = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
        var old = now.AddDays(-40);
        var recent = now.AddDays(-2);
        Assert.True(Should("/bak", BackupKinds.Scheduled, "UnAccessDB_20200101_020000.bak", old));
        Assert.False(Should("/bak", BackupKinds.Manual, "UnAccessDB_20200101_030000.bak", old));
        Assert.False(Should("/bak", BackupKinds.PreRestoreSafety, "UnAccessDB_PreRestore_20200101_030000.bak", old));
        Assert.False(Should("/bak", BackupKinds.Scheduled, "notes.bak", old));
        Assert.False(Should("/other", BackupKinds.Scheduled, "UnAccessDB_20200101_020000.bak", old));
        Assert.False(Should("/bak", BackupKinds.Scheduled, "UnAccessDB_20261013_020000.bak", recent));

        bool Should(string directory, string kind, string file, DateTime completed) =>
            BackupRetention.ShouldDelete(new BackupRetentionCandidate
            {
                BackupType = kind,
                Status = BackupStatuses.Completed,
                FileName = file,
                BackupDirectory = directory,
                CompletedAtUtc = completed
            }, "/bak", now, 30);
    }

    [Fact]
    public void Sanitizer_removes_passwords_connection_strings_hashes_and_controller_secrets()
    {
        var password = SensitiveDataSanitizer.Clean("password=hunter2");
        Assert.DoesNotContain("hunter2", password, StringComparison.Ordinal);
        Assert.Contains("[gizlendi]", password, StringComparison.Ordinal);

        var connection = SensitiveDataSanitizer.Clean("Server=sql.local;User ID=sa;Password=hunter2;Initial Catalog=UnAccessDB");
        Assert.DoesNotContain("sql.local", connection, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", connection, StringComparison.Ordinal);

        var hash = SensitiveDataSanitizer.Clean("AQAAAAIAAYagAAAAEHb1234567890abcdefghij");
        Assert.DoesNotContain("Hb1234567890abcdefghij", hash, StringComparison.Ordinal);
        Assert.Contains("[hash gizlendi]", hash, StringComparison.Ordinal);

        var controller = SensitiveDataSanitizer.Clean("controllerpassword=abc controlleruser=root token=sekret");
        Assert.DoesNotContain("abc", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("root", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("sekret", controller, StringComparison.Ordinal);

        var login = SensitiveDataSanitizer.Clean("Login failed for user 'sa'.");
        Assert.DoesNotContain("'sa'", login, StringComparison.Ordinal);
    }

    [Fact]
    public void File_log_does_not_keep_a_connection_string_or_password()
    {
        var root = Path.Combine(Path.GetTempPath(), "unaccess-log-" + Guid.NewGuid().ToString("N"));
        var paths = new FixedPaths(root);
        var provider = new SanitizingFileLoggerProvider(paths, LogLevel.Information);
        var logger = provider.CreateLogger("test");
        logger.LogError("Bağlantı denemesi {Detail}", "Server=sql.local;User ID=sa;Password=hunter2");
        logger.LogError("Hash {Value}", "AQAAAAIAAYagAAAAEHb1234567890abcdefghij");
        var file = Assert.Single(Directory.GetFiles(paths.LogDirectory, "unaccess-*.log"));
        var text = File.ReadAllText(file);
        Assert.DoesNotContain("hunter2", text, StringComparison.Ordinal);
        Assert.DoesNotContain("sql.local", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Hb1234567890abcdefghij", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Protected_store_round_trips_without_plaintext()
    {
        var root = Path.Combine(Path.GetTempPath(), "unaccess-cfg-" + Guid.NewGuid().ToString("N"));
        var store = new FileConnectionConfigurationStore(new FixedPaths(root), new XorProtector());
        var secret = "Data Source=db.local;Initial Catalog=UnAccess_lab;User ID=app;Password=S3cret-Token-Value";
        store.Save(secret);
        var read = store.Read();
        Assert.True(read.Success);
        Assert.Equal(secret, read.ConnectionString);
        var stored = Encoding.Latin1.GetString(File.ReadAllBytes(Path.Combine(root, "Configuration", "connection.protected")));
        Assert.DoesNotContain("S3cret-Token-Value", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("db.local", stored, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, stored, StringComparison.Ordinal);
    }

    [Fact]
    public void Protected_store_reports_unreadable_secret_for_the_wrong_protector()
    {
        var root = Path.Combine(Path.GetTempPath(), "unaccess-cfg-" + Guid.NewGuid().ToString("N"));
        var paths = new FixedPaths(root);
        new FileConnectionConfigurationStore(paths, new XorProtector()).Save("Data Source=db.local;Initial Catalog=UnAccess_lab;User ID=app;Password=S3cret-Token-Value");
        var read = new FileConnectionConfigurationStore(paths, new BrokenProtector()).Read();
        Assert.False(read.Success);
        Assert.Equal(BackupMessages.ProtectedUnreadable, read.Error);
        Assert.Null(read.ConnectionString);
    }

    [Fact]
    public void Windows_protector_refuses_to_run_off_windows()
    {
        var protector = new DpapiSecretProtector();
        if (OperatingSystem.IsWindows())
        {
            var plain = new byte[] { 7, 8, 9 };
            Assert.Equal(plain, protector.Unprotect(protector.Protect(plain)));
            return;
        }

        var exception = Assert.Throws<UnAccessException>(() => protector.Protect(new byte[] { 1 }));
        Assert.Equal(BackupMessages.ProtectedUnsupported, exception.UserMessage);
    }

    [Fact]
    public void Version_provider_reads_assembly_metadata()
    {
        var environment = new UnAccessEnvironment(new ConfigurationBuilder().Build());
        var provider = new ApplicationVersionProvider(environment);
        var assembly = typeof(BackupMessages).Assembly;
        Assert.Equal(assembly.GetName().Version!.ToString(), provider.Version);
        Assert.False(string.IsNullOrWhiteSpace(provider.InformationalVersion));
        Assert.Equal("Development", provider.EnvironmentName);
    }

    [Fact]
    public async Task Update_service_is_not_configured()
    {
        var version = new ApplicationVersionProvider(new UnAccessEnvironment(new ConfigurationBuilder().Build()));
        var result = await new NotConfiguredUpdateService(version).CheckAsync(CancellationToken.None);
        Assert.Equal("NotConfigured", result.Status);
        Assert.Equal(BackupMessages.UpdateNotConfigured, result.Message);
        Assert.False(result.IsUpdateAvailable);
        Assert.Null(result.LatestVersion);
        Assert.Null(result.DownloadUri);
    }

    [Fact]
    public void Readiness_is_not_complete_when_any_item_is_an_error()
    {
        var failed = new ReadinessReport
        {
            Items = [new ReadinessItem { Level = ReadinessLevels.Error }, new ReadinessItem { Level = ReadinessLevels.Ok }]
        };
        var warning = new ReadinessReport
        {
            Items = [new ReadinessItem { Level = ReadinessLevels.Warning }, new ReadinessItem { Level = ReadinessLevels.Ok }]
        };
        Assert.False(failed.IsComplete);
        Assert.True(warning.IsComplete);
    }

    [Fact]
    public void Managed_password_hash_matches_identity_v3()
    {
        var passwords = new IdentityPasswordHasher();
        var password = "UnAccess.2026";
        var fromIdentity = passwords.Hash(password);
        var fromManaged = ManagedIdentityPassword.Hash(password);

        Assert.StartsWith("AQAAAA", fromManaged, StringComparison.Ordinal);
        Assert.True(ManagedIdentityPassword.Verify(fromIdentity, password));
        Assert.True(passwords.Verify(fromManaged, password));
        Assert.False(ManagedIdentityPassword.Verify(fromIdentity, "UnAccess.2027"));
        Assert.False(passwords.Verify(fromManaged, "wrong-password"));
    }

    [Fact]
    public void Hospital_clock_accepts_iana_and_windows_timezone_ids()
    {
        var iana = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Hospital:TimeZoneId"] = "Europe/Istanbul"
        }).Build();
        var windows = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Hospital:TimeZoneId"] = "Turkey Standard Time"
        }).Build();

        var fromIana = new HospitalClock(iana);
        var fromWindows = new HospitalClock(windows);

        Assert.Equal(TimeSpan.FromHours(3), fromIana.Zone.BaseUtcOffset);
        Assert.Equal(TimeSpan.FromHours(3), fromWindows.Zone.BaseUtcOffset);
        Assert.Throws<UnAccessException>(() => new HospitalClock(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Hospital:TimeZoneId"] = "Not/AZone"
        }).Build()));
    }

    [Fact]
    public void Production_source_does_not_enable_sensitive_logging_or_hardcode_a_version_label()
    {
        var root = Repo.Root();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
            Assert.DoesNotContain("EnableSensitiveDataLogging", File.ReadAllText(file), StringComparison.Ordinal);

        foreach (var file in Directory.GetFiles(Path.Combine(root, "src", "UnAccess.Desktop", "Forms"), "*.cs"))
            Assert.DoesNotContain("v1.0.0", File.ReadAllText(file), StringComparison.Ordinal);
    }

    private sealed class FixedPaths : IAppPathProvider
    {
        public FixedPaths(string root)
        {
            Root = root;
            LogDirectory = Path.Combine(root, "Logs");
            ConfigurationDirectory = Path.Combine(root, "Configuration");
            Directory.CreateDirectory(LogDirectory);
            Directory.CreateDirectory(ConfigurationDirectory);
        }

        public string Root { get; }
        public string LogDirectory { get; }
        public string ConfigurationDirectory { get; }
    }

    private sealed class XorProtector : ISecretProtector
    {
        public byte[] Protect(byte[] plaintext) => plaintext.Select(item => (byte)(item ^ 0x5A)).ToArray();
        public byte[] Unprotect(byte[] ciphertext) => ciphertext.Select(item => (byte)(item ^ 0x5A)).ToArray();
    }

    private sealed class BrokenProtector : ISecretProtector
    {
        public byte[] Protect(byte[] plaintext) => plaintext;
        public byte[] Unprotect(byte[] ciphertext) => throw new CryptographicException("wrong machine");
    }
}
