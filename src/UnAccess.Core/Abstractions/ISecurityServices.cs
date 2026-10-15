using UnAccess.Core.Models;

namespace UnAccess.Core.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    int? UserId { get; }
    string? Username { get; }
    string? FullName { get; }
    bool MustChangePassword { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> Permissions { get; }
    void SignIn(UserSession session);
    void SignOut();
    void CompletePasswordChange();
}

public interface IAccessGuard
{
    bool Can(string permission);
    void Require(string permission);
    void Demand(string permission);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public interface IAccountService
{
    Task<bool> HasAnyUserAsync(CancellationToken cancellationToken);
    Task<LoginResult> CreateInitialAdminAsync(InitialAdminModel model, CancellationToken cancellationToken);
    Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken);
    Task ChangeOwnPasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken);
}

public interface IUserAdminService
{
    Task<IReadOnlyList<UserListItem>> ListAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<RoleChoice>> ListRolesAsync(CancellationToken cancellationToken);
    Task CreateAsync(UserWriteModel model, CancellationToken cancellationToken);
    Task UpdateAsync(int id, UserWriteModel model, CancellationToken cancellationToken);
    Task SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken);
    Task ResetPasswordAsync(int id, string temporaryPassword, CancellationToken cancellationToken);
}

public interface IRuntimeSettings
{
    Task<int> LoginMaxFailedAttemptsAsync(CancellationToken cancellationToken);
    Task<int> LoginLockoutMinutesAsync(CancellationToken cancellationToken);
    Task<int> PasswordMinLengthAsync(CancellationToken cancellationToken);
    Task<int> DeviceSyncBatchSizeAsync(CancellationToken cancellationToken);
    Task<TimeSpan> DeviceSyncStaleProcessingAsync(CancellationToken cancellationToken);
    Task<TimeSpan> HeartbeatIntervalAsync(CancellationToken cancellationToken);
    Task<TimeSpan> HeartbeatStaleAsync(CancellationToken cancellationToken);
    Task<string> HospitalNameAsync(CancellationToken cancellationToken);
    Task<string> TimeZoneIdAsync(CancellationToken cancellationToken);
    Task<bool> BackupEnabledAsync(CancellationToken cancellationToken);
    Task<string> BackupDirectoryAsync(CancellationToken cancellationToken);
    Task<TimeOnly> BackupTimeAsync(CancellationToken cancellationToken);
    Task<int> BackupRetentionDaysAsync(CancellationToken cancellationToken);
}

public interface ISystemSettingService
{
    Task<IReadOnlyList<SystemSettingItem>> ListAsync(CancellationToken cancellationToken);
    Task SaveAsync(IReadOnlyList<SystemSettingItem> items, CancellationToken cancellationToken);
}

public interface IServiceMonitor
{
    Task BeatAsync(ServicePulse pulse, CancellationToken cancellationToken);
}

public interface ISystemHealthService
{
    Task<SystemHealthReport> GetAsync(CancellationToken cancellationToken);
}

public interface IAuditQueryService
{
    Task<ReportPage<AuditLogRow>> SearchAsync(AuditQuery query, CancellationToken cancellationToken);
    Task<AuditLogDetail?> GetAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<LookupItem>> ListUsersAsync(CancellationToken cancellationToken);
}

public interface IReportService
{
    Task<IReadOnlyList<LookupItem>> PersonnelOptionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<LookupItem>> DepartmentOptionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<LookupItem>> DoorOptionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<LookupItem>> ZoneOptionsAsync(CancellationToken cancellationToken);
    Task<ReportPage<PassageReportRow>> PassagesAsync(PassageReportQuery query, CancellationToken cancellationToken);
    Task WritePassagesCsvAsync(PassageReportQuery query, Stream output, CancellationToken cancellationToken);
    Task<ReportPage<PersonnelAccessReportRow>> PersonnelAccessAsync(AccessReportQuery query, CancellationToken cancellationToken);
    Task WritePersonnelAccessCsvAsync(AccessReportQuery query, Stream output, CancellationToken cancellationToken);
    Task<ReportPage<DoorAccessReportRow>> DoorAccessAsync(AccessReportQuery query, CancellationToken cancellationToken);
    Task WriteDoorAccessCsvAsync(AccessReportQuery query, Stream output, CancellationToken cancellationToken);
}
