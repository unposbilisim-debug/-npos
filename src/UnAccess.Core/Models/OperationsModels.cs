using UnAccess.Core.Backup;

namespace UnAccess.Core.Models;

public sealed class BackupResult
{
    public bool Succeeded { get; init; }
    public string Message { get; init; } = "";
    public string? FileName { get; init; }
    public string? FullPath { get; init; }
}

public sealed class BackupStatusView
{
    public bool Enabled { get; init; }
    public string Directory { get; init; } = "";
    public string TimeText { get; init; } = "";
    public int RetentionDays { get; init; }
    public string StatusText { get; init; } = "";
    public string LastSuccessText { get; init; } = "—";
    public string LastFileName { get; init; } = "—";
    public string FolderText { get; init; } = "Bilinmiyor";
}

public sealed class BackupInspection
{
    public bool CanRead { get; init; }
    public string Message { get; init; } = "";
    public string? SourceDatabase { get; init; }
    public bool HasBackupPermission { get; init; }
    public bool HasRestorePermission { get; init; }
}

public sealed class RestoreRequest
{
    public string BackupFile { get; init; } = "";
    public string ConfirmationText { get; init; } = "";
}

public sealed class RestoreResult
{
    public bool Succeeded { get; init; }
    public string Message { get; init; } = "";
    public string? SafetyBackupFileName { get; init; }
}

public sealed class SqlEndpoint
{
    public string Server { get; init; } = "";
    public string? Instance { get; init; }
    public string Database { get; init; } = "";
    public bool IntegratedSecurity { get; init; }
    public string? UserName { get; init; }
    public string? Password { get; init; }
}

public sealed class ConnectionProbeResult
{
    public bool Succeeded { get; init; }
    public string Message { get; init; } = "";
}

public sealed class ConnectionConfigurationRead
{
    public bool Success { get; init; }
    public string? ConnectionString { get; init; }
    public string? Error { get; init; }
}

public sealed class UpdateCheckResult
{
    public string CurrentVersion { get; init; } = "";
    public string? LatestVersion { get; init; }
    public bool IsUpdateAvailable { get; init; }
    public string? ReleaseNotes { get; init; }
    public string? DownloadUri { get; init; }
    public string Status { get; init; } = "NotConfigured";
    public string Message { get; init; } = "";
}

public sealed class ReadinessItem
{
    public string Name { get; init; } = "";
    public string Level { get; init; } = "";
    public string Detail { get; init; } = "";
}

public sealed class ReadinessReport
{
    public IReadOnlyList<ReadinessItem> Items { get; init; } = [];
    public bool IsComplete => Items.All(item => item.Level != ReadinessLevels.Error);
}

public sealed class InstallationSettings
{
    public string HospitalName { get; init; } = "";
    public string TimeZoneId { get; init; } = "";
    public bool BackupEnabled { get; init; }
    public string BackupDirectory { get; init; } = "";
    public string BackupTime { get; init; } = "02:00";
    public int BackupRetentionDays { get; init; } = 30;
}

public sealed class PathProbeResult
{
    public bool SeenBySql { get; init; }
    public bool Verified { get; init; }
    public string Message { get; init; } = "Bilinmiyor";
}
