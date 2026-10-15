namespace UnAccess.Core.Security;

public static class RoleCodes
{
    public const string Admin = "ADMIN";
    public const string SecurityCenter = "GUVENLIK_MERKEZI";
    public const string HumanResources = "IK";
    public const string AccessManager = "YETKI_YONETICISI";
    public const string Observer = "IZLEME";
}

public static class Permissions
{
    public const string PersonnelView = "personnel.view";
    public const string PersonnelManage = "personnel.manage";
    public const string CardsView = "cards.view";
    public const string CardsManage = "cards.manage";
    public const string AccessView = "access.view";
    public const string AccessManage = "access.manage";
    public const string HospitalView = "hospital.view";
    public const string HospitalManage = "hospital.manage";
    public const string DevicesView = "devices.view";
    public const string DevicesOperate = "devices.operate";
    public const string LiveView = "live.view";
    public const string PassageSimulate = "passage.simulate";
    public const string ReportsView = "reports.view";
    public const string UsersManage = "users.manage";
    public const string AuditView = "audit.view";
    public const string SettingsManage = "settings.manage";
    public const string SystemView = "system.view";

    public static readonly string[] All =
    [
        PersonnelView, PersonnelManage, CardsView, CardsManage, AccessView, AccessManage,
        HospitalView, HospitalManage, DevicesView, DevicesOperate, LiveView, PassageSimulate,
        ReportsView, UsersManage, AuditView, SettingsManage, SystemView
    ];

    public static IReadOnlySet<string> ForRoles(IEnumerable<string> roles)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            if (Grants.TryGetValue(role, out var permissions))
            {
                foreach (var permission in permissions)
                    set.Add(permission);
            }
        }

        return set;
    }

    private static readonly Dictionary<string, string[]> Grants = new(StringComparer.Ordinal)
    {
        [RoleCodes.Admin] = All,
        [RoleCodes.SecurityCenter] =
        [
            DevicesView, DevicesOperate, LiveView, PassageSimulate, ReportsView, AuditView, SystemView
        ],
        [RoleCodes.HumanResources] = [PersonnelView, PersonnelManage, CardsView, CardsManage],
        [RoleCodes.AccessManager] = [AccessView, AccessManage],
        [RoleCodes.Observer] =
        [
            PersonnelView, CardsView, AccessView, HospitalView, DevicesView, LiveView, ReportsView, AuditView, SystemView
        ]
    };
}

public static class SecurityEventTypes
{
    public const string LoginSuccess = "LoginSuccess";
    public const string LoginFailed = "LoginFailed";
    public const string AccountLocked = "AccountLocked";
    public const string PasswordChanged = "PasswordChanged";
    public const string PasswordReset = "PasswordReset";
    public const string UserActivated = "UserActivated";
    public const string UserDeactivated = "UserDeactivated";
}

public static class SettingKeys
{
    public const string HospitalName = "HospitalName";
    public const string TimeZone = "TimeZone";
    public const string LoginMaxFailedAttempts = "LoginMaxFailedAttempts";
    public const string LoginLockoutMinutes = "LoginLockoutMinutes";
    public const string PasswordMinLength = "PasswordMinLength";
    public const string DeviceSyncBatchSize = "DeviceSyncBatchSize";
    public const string DeviceSyncStaleProcessingMinutes = "DeviceSyncStaleProcessingMinutes";
    public const string ServiceHeartbeatIntervalSeconds = "ServiceHeartbeatIntervalSeconds";
    public const string ServiceHeartbeatStaleSeconds = "ServiceHeartbeatStaleSeconds";

    public static readonly string[] Editable =
    [
        HospitalName, TimeZone, LoginMaxFailedAttempts, LoginLockoutMinutes, PasswordMinLength,
        DeviceSyncBatchSize, DeviceSyncStaleProcessingMinutes, ServiceHeartbeatIntervalSeconds,
        ServiceHeartbeatStaleSeconds
    ];
}

public static class SecurityDefaults
{
    public const int MaxFailedAttempts = 5;
    public const int LockoutMinutes = 15;
    public const int PasswordMinLength = 10;
    public const int HeartbeatIntervalSeconds = 30;
    public const int HeartbeatStaleSeconds = 90;
    public const string HospitalName = "Hastane";
    public const string TimeZone = "Europe/Istanbul";
}

public static class ServiceNames
{
    public const string DeviceSync = "DeviceSync";
}

public static class HeartbeatStatus
{
    public const string Running = "Çalışıyor";
    public const string NotResponding = "Çalışmıyor / Yanıt Yok";

    public static bool IsAlive(DateTime lastSeenUtc, DateTime utcNow, TimeSpan stale) =>
        utcNow - lastSeenUtc <= stale;
}

public static class ReportPaging
{
    public const int DefaultSize = 100;

    public static int NormalizeSize(int size) => size is 50 or 250 ? size : DefaultSize;

    public static int NormalizePage(int page) => page < 1 ? 1 : page;
}

public static class LoginMessages
{
    public const string InvalidCredentials = "Kullanıcı adı veya şifre hatalı.";
    public const string Locked = "Hesap geçici olarak kilitlendi. Daha sonra tekrar deneyin.";
}
