using UnAccess.Core.Models;

namespace UnAccess.Core.Abstractions;

public interface IUnAccessEnvironment
{
    string Name { get; }
    bool IsProduction { get; }
    bool AllowsDevelopmentSeed { get; }
    bool AllowsAutomaticMigration { get; }
}

public interface IAppPathProvider
{
    string Root { get; }
    string LogDirectory { get; }
    string ConfigurationDirectory { get; }
}

public interface ISecretProtector
{
    byte[] Protect(byte[] plaintext);
    byte[] Unprotect(byte[] ciphertext);
}

public interface IConnectionConfigurationStore
{
    void Save(string connectionString);
    ConnectionConfigurationRead Read();
}

public interface IApplicationVersionProvider
{
    string Version { get; }
    string InformationalVersion { get; }
    string EnvironmentName { get; }
}

public interface IUpdateCheckService
{
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken);
}

public interface IMaintenanceGate
{
    Task<IAsyncDisposable?> TryEnterExclusiveAsync(CancellationToken cancellationToken);
    Task<IAsyncDisposable?> TryEnterSharedAsync(CancellationToken cancellationToken);
}

public interface IBackupService
{
    Task<BackupResult> CreateManualAsync(CancellationToken cancellationToken);
    Task<BackupResult> CreateScheduledAsync(CancellationToken cancellationToken);
    Task<BackupStatusView> GetStatusAsync(CancellationToken cancellationToken);
    Task ApplyRetentionAsync(CancellationToken cancellationToken);
    Task RecoverStaleAsync(CancellationToken cancellationToken);
    Task<PathProbeResult> ProbeDirectoryAsync(string directory, CancellationToken cancellationToken);
}

public interface IRestoreService
{
    Task<BackupInspection> InspectAsync(string backupFile, CancellationToken cancellationToken);
    Task<RestoreResult> RestoreAsync(RestoreRequest request, CancellationToken cancellationToken);
}

public interface IDatabaseHealthService
{
    Task<DatabaseHealthReport> GetAsync(CancellationToken cancellationToken);
}

public interface IInstallationReadinessService
{
    Task<ReadinessReport> EvaluateAsync(CancellationToken cancellationToken);
}

public interface IConnectionProbe
{
    Task<ConnectionProbeResult> TestAsync(SqlEndpoint endpoint, CancellationToken cancellationToken);
    string BuildConnectionString(SqlEndpoint endpoint);
}

public interface ISetupService
{
    Task ApplySchemaAsync(CancellationToken cancellationToken);
    Task SaveInstallationSettingsAsync(InstallationSettings settings, CancellationToken cancellationToken);
}

public sealed class DatabaseHealthReport
{
    public bool Connected { get; init; }
    public string ConnectionText { get; init; } = "Bilinmiyor";
    public bool PendingMigrations { get; init; }
    public string MigrationText { get; init; } = "Bilinmiyor";
    public string ModelText { get; init; } = "Bilinmiyor";
    public bool TablesReadable { get; init; }
}
