using Microsoft.EntityFrameworkCore;
using UnAccess.Core.Entities;
using UnAccess.Core.Security;

namespace UnAccess.Data.Infrastructure;

internal static class SecurityBootstrap
{
    public static async Task EnsureAsync(UnAccessDbContext db, DateTime utcNow, CancellationToken cancellationToken)
    {
        foreach (var (code, name, description) in Roles)
        {
            var existing = await db.Roles.FirstOrDefaultAsync(role => role.Code == code, cancellationToken);
            if (existing != null)
                continue;

            db.Roles.Add(new Role
            {
                Code = code,
                Name = name,
                Description = description,
                IsActive = true
            });
        }

        foreach (var (key, value) in Defaults)
        {
            var existing = await db.SystemSettings.FirstOrDefaultAsync(setting => setting.Key == key, cancellationToken);
            if (existing != null)
                continue;

            db.SystemSettings.Add(new SystemSetting
            {
                Key = key,
                Value = value,
                UpdatedAtUtc = utcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static readonly (string Code, string Name, string Description)[] Roles =
    [
        (RoleCodes.Admin, "Yönetici", "Tüm işlemler"),
        (RoleCodes.SecurityCenter, "Güvenlik Merkezi", "Canlı geçiş, kontrolör ve senkronizasyon"),
        (RoleCodes.HumanResources, "İnsan Kaynakları", "Personel ve kart işlemleri"),
        (RoleCodes.AccessManager, "Yetki Yöneticisi", "Yetki grupları, zaman planları ve istisnalar"),
        (RoleCodes.Observer, "İzleme", "Yalnızca görüntüleme")
    ];

    private static readonly (string Key, string Value)[] Defaults =
    [
        (SettingKeys.HospitalName, SecurityDefaults.HospitalName),
        (SettingKeys.TimeZone, SecurityDefaults.TimeZone),
        (SettingKeys.LoginMaxFailedAttempts, SecurityDefaults.MaxFailedAttempts.ToString()),
        (SettingKeys.LoginLockoutMinutes, SecurityDefaults.LockoutMinutes.ToString()),
        (SettingKeys.PasswordMinLength, SecurityDefaults.PasswordMinLength.ToString()),
        (SettingKeys.DeviceSyncBatchSize, "50"),
        (SettingKeys.DeviceSyncStaleProcessingMinutes, "5"),
        (SettingKeys.ServiceHeartbeatIntervalSeconds, SecurityDefaults.HeartbeatIntervalSeconds.ToString()),
        (SettingKeys.ServiceHeartbeatStaleSeconds, SecurityDefaults.HeartbeatStaleSeconds.ToString())
    ];
}
