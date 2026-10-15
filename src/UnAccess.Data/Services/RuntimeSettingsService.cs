using Microsoft.EntityFrameworkCore;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Audit;
using UnAccess.Core.Common;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Core.Security;
using UnAccess.Core.Sync;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Data.Services;

internal sealed class RuntimeSettingsService : IRuntimeSettings, ISystemSettingService
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly IUtcClock _clock;
    private readonly ICurrentUser _current;
    private readonly IAccessGuard _guard;
    private readonly IHospitalClock _hospitalClock;
    private readonly AuditWriter _audit;
    private readonly DeviceSyncOptions _sync;

    public RuntimeSettingsService(
        IDbContextFactory<UnAccessDbContext> factory,
        IUtcClock clock,
        ICurrentUser current,
        IAccessGuard guard,
        IHospitalClock hospitalClock,
        AuditWriter audit,
        DeviceSyncOptions sync)
    {
        _factory = factory;
        _clock = clock;
        _current = current;
        _guard = guard;
        _hospitalClock = hospitalClock;
        _audit = audit;
        _sync = sync;
    }

    public Task<int> LoginMaxFailedAttemptsAsync(CancellationToken cancellationToken) =>
        ReadIntAsync(SettingKeys.LoginMaxFailedAttempts, SecurityDefaults.MaxFailedAttempts, 1, 20, cancellationToken);

    public Task<int> LoginLockoutMinutesAsync(CancellationToken cancellationToken) =>
        ReadIntAsync(SettingKeys.LoginLockoutMinutes, SecurityDefaults.LockoutMinutes, 1, 1440, cancellationToken);

    public Task<int> PasswordMinLengthAsync(CancellationToken cancellationToken) =>
        ReadIntAsync(SettingKeys.PasswordMinLength, SecurityDefaults.PasswordMinLength, 8, 128, cancellationToken);

    public Task<int> DeviceSyncBatchSizeAsync(CancellationToken cancellationToken) =>
        ReadIntAsync(SettingKeys.DeviceSyncBatchSize, _sync.BatchSize, 1, 500, cancellationToken);

    public async Task<TimeSpan> DeviceSyncStaleProcessingAsync(CancellationToken cancellationToken)
    {
        var minutes = await ReadIntAsync(SettingKeys.DeviceSyncStaleProcessingMinutes, (int)_sync.StaleProcessing.TotalMinutes, 1, 1440, cancellationToken);
        return TimeSpan.FromMinutes(minutes);
    }

    public async Task<TimeSpan> HeartbeatIntervalAsync(CancellationToken cancellationToken)
    {
        var seconds = await ReadIntAsync(SettingKeys.ServiceHeartbeatIntervalSeconds, SecurityDefaults.HeartbeatIntervalSeconds, 5, 300, cancellationToken);
        return TimeSpan.FromSeconds(seconds);
    }

    public async Task<TimeSpan> HeartbeatStaleAsync(CancellationToken cancellationToken)
    {
        var seconds = await ReadIntAsync(SettingKeys.ServiceHeartbeatStaleSeconds, SecurityDefaults.HeartbeatStaleSeconds, 15, 3600, cancellationToken);
        return TimeSpan.FromSeconds(seconds);
    }

    public async Task<string> HospitalNameAsync(CancellationToken cancellationToken)
    {
        var value = await ReadAsync(SettingKeys.HospitalName, cancellationToken);
        return string.IsNullOrWhiteSpace(value) ? SecurityDefaults.HospitalName : value.Trim();
    }

    public async Task<string> TimeZoneIdAsync(CancellationToken cancellationToken)
    {
        var value = await ReadAsync(SettingKeys.TimeZone, cancellationToken);
        return string.IsNullOrWhiteSpace(value) ? SecurityDefaults.TimeZone : value.Trim();
    }

    public async Task<IReadOnlyList<SystemSettingItem>> ListAsync(CancellationToken cancellationToken)
    {
        _guard.Demand(Permissions.SettingsManage);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var stored = await db.SystemSettings.AsNoTracking().ToDictionaryAsync(item => item.Key, item => item.Value, cancellationToken);
        return SettingKeys.Editable.Select(key => new SystemSettingItem
        {
            Key = key,
            Value = stored.TryGetValue(key, out var value) ? value : ""
        }).ToList();
    }

    public async Task SaveAsync(IReadOnlyList<SystemSettingItem> items, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        _guard.Demand(Permissions.SettingsManage);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var now = _clock.UtcNow;
        foreach (var item in items)
        {
            if (!SettingKeys.Editable.Contains(item.Key))
                throw new UnAccessException("Bu ayar değiştirilemez.");

            var value = Validate(item.Key, item.Value);
            var row = await db.SystemSettings.FirstOrDefaultAsync(setting => setting.Key == item.Key, cancellationToken);
            var previous = row?.Value;
            if (row == null)
            {
                row = new Core.Entities.SystemSetting { Key = item.Key, Value = value, UpdatedAtUtc = now, UpdatedByUserId = _current.UserId };
                db.SystemSettings.Add(row);
            }
            else if (row.Value == value)
            {
                continue;
            }
            else
            {
                row.Value = value;
                row.UpdatedAtUtc = now;
                row.UpdatedByUserId = _current.UserId;
            }

            if (item.Key == SettingKeys.TimeZone)
                _hospitalClock.ApplyTimeZone(value);

            _audit.Add(db, new AuditEntry
            {
                Action = AuditActions.Updated,
                EntityType = AuditEntities.SystemSetting,
                EntityId = item.Key,
                Description = $"Sistem ayarı güncellendi: {item.Key}",
                OldValue = previous,
                NewValue = value
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> ReadIntAsync(string key, int fallback, int min, int max, CancellationToken cancellationToken)
    {
        var text = await ReadAsync(key, cancellationToken);
        if (int.TryParse(text, out var value) && value >= min && value <= max)
            return value;
        return fallback < min ? min : fallback > max ? max : fallback;
    }

    private async Task<string?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.SystemSettings.AsNoTracking().Where(item => item.Key == key).Select(item => item.Value).FirstOrDefaultAsync(cancellationToken);
    }

    private static string Validate(string key, string? value)
    {
        var text = (value ?? "").Trim();
        if (text.Length == 0 || text.Length > FieldLengths.SettingValue || text.Any(char.IsControl))
            throw new UnAccessException("Ayar değeri geçersiz.");

        switch (key)
        {
            case SettingKeys.HospitalName:
                return TextRules.Name(text, "Hastane adı");
            case SettingKeys.TimeZone:
                try
                {
                    _ = TimeZoneInfo.FindSystemTimeZoneById(text);
                }
                catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
                {
                    throw new UnAccessException("Saat dilimi bulunamadı.");
                }

                return text;
            case SettingKeys.LoginMaxFailedAttempts:
                return RequireInt(text, 1, 20);
            case SettingKeys.LoginLockoutMinutes:
            case SettingKeys.DeviceSyncStaleProcessingMinutes:
                return RequireInt(text, 1, 1440);
            case SettingKeys.PasswordMinLength:
                return RequireInt(text, 8, 128);
            case SettingKeys.DeviceSyncBatchSize:
                return RequireInt(text, 1, 500);
            case SettingKeys.ServiceHeartbeatIntervalSeconds:
                return RequireInt(text, 5, 300);
            case SettingKeys.ServiceHeartbeatStaleSeconds:
                return RequireInt(text, 15, 3600);
            default:
                throw new UnAccessException("Bu ayar değiştirilemez.");
        }
    }

    private static string RequireInt(string text, int min, int max)
    {
        if (!int.TryParse(text, out var value) || value < min || value > max)
            throw new UnAccessException($"Değer {min} ile {max} arasında olmalıdır.");
        return value.ToString();
    }
}
