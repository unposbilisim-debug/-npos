namespace UnAccess.Core.Models;

public sealed class UserSession
{
    public int UserId { get; init; }
    public string Username { get; init; } = "";
    public string FullName { get; init; } = "";
    public bool MustChangePassword { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public IReadOnlyList<string> Permissions { get; init; } = [];
}

public sealed class LoginResult
{
    public bool Succeeded { get; init; }
    public bool MustChangePassword { get; init; }
    public string? Message { get; init; }
    public UserSession? Session { get; init; }
}

public sealed class InitialAdminModel
{
    public string Username { get; init; } = "";
    public string FullName { get; init; } = "";
    public string Password { get; init; } = "";
}

public sealed class UserWriteModel
{
    public string Username { get; init; } = "";
    public string FullName { get; init; } = "";
    public string? Password { get; init; }
    public bool IsActive { get; init; } = true;
    public IReadOnlyList<string> RoleCodes { get; init; } = [];
}

public sealed class UserListItem
{
    public int Id { get; init; }
    public string Username { get; init; } = "";
    public string FullName { get; init; } = "";
    public string Roles { get; init; } = "";
    public bool IsActive { get; init; }
    public bool MustChangePassword { get; init; }
    public DateTime? LastLoginAtUtc { get; init; }
    public IReadOnlyList<string> RoleCodes { get; init; } = [];
}

public sealed class RoleChoice
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
}

public sealed class SystemSettingItem
{
    public string Key { get; init; } = "";
    public string Value { get; init; } = "";
}

public sealed class ServicePulse
{
    public string ServiceName { get; init; } = "";
    public string InstanceId { get; init; } = "";
    public DateTime StartedAtUtc { get; init; }
    public string Version { get; init; } = "";
}

public sealed class SystemHealthReport
{
    public bool DatabaseConnected { get; init; }
    public string DatabaseText { get; init; } = "";
    public string DeviceServiceText { get; init; } = "";
    public DateTime? DeviceServiceLastSeenUtc { get; init; }
    public int ControllersOnline { get; init; }
    public int ControllersOffline { get; init; }
    public int ControllersUnknown { get; init; }
    public int SyncPending { get; init; }
    public int SyncFailed { get; init; }
    public int SyncDeadLetter { get; init; }
    public DateTime? LastAccessEventUtc { get; init; }
    public string MigrationText { get; init; } = "Bilinmiyor";
    public string ModelText { get; init; } = "Bilinmiyor";
    public string BackupStatusText { get; init; } = "Bilinmiyor";
    public DateTime? LastSuccessfulBackupUtc { get; init; }
    public DateTime? LastFailedBackupUtc { get; init; }
    public string BackupDirectory { get; init; } = "";
    public string BackupFolderText { get; init; } = "Bilinmiyor";
    public string DiskSpaceText { get; init; } = "Bilinmiyor";
    public bool BackupEnabled { get; init; }
    public string BackupTimeText { get; init; } = "";
    public int BackupRetentionDays { get; init; }
}

public sealed class ReportPage<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public sealed class AuditQuery
{
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public int? UserId { get; init; }
    public string? Action { get; init; }
    public string? EntityType { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 100;
}

public sealed class AuditLogRow
{
    public int Id { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public string UserName { get; init; } = "";
    public string Action { get; init; } = "";
    public string EntityType { get; init; } = "";
    public string Description { get; init; } = "";
    public string ComputerName { get; init; } = "";
}

public sealed class AuditLogDetail
{
    public int Id { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public string Description { get; init; } = "";
}

public sealed class PassageReportQuery
{
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public int? PersonnelId { get; init; }
    public int? DepartmentId { get; init; }
    public int? DoorId { get; init; }
    public int? ZoneId { get; init; }
    public bool DeniedOnly { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 100;
}

public sealed class AccessReportQuery
{
    public int? PersonnelId { get; init; }
    public int? DepartmentId { get; init; }
    public int? DoorId { get; init; }
    public int? ZoneId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 100;
}

public sealed class PassageReportRow
{
    public int Id { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public string PersonnelName { get; init; } = "";
    public string DepartmentName { get; init; } = "";
    public string CardNumber { get; init; } = "";
    public string DoorName { get; init; } = "";
    public string ZoneName { get; init; } = "";
    public string Decision { get; init; } = "";
    public string Reason { get; init; } = "";
}

public sealed class PersonnelAccessReportRow
{
    public string PersonnelNumber { get; init; } = "";
    public string PersonnelName { get; init; } = "";
    public string DepartmentName { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Target { get; init; } = "";
    public string Schedule { get; init; } = "";
    public string Effect { get; init; } = "";
}

public sealed class DoorAccessReportRow
{
    public string DoorName { get; init; } = "";
    public string ZoneName { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Schedule { get; init; } = "";
    public string Effect { get; init; } = "";
}
