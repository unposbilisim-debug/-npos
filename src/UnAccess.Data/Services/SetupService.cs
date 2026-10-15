using Microsoft.EntityFrameworkCore;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Audit;
using UnAccess.Core.Backup;
using UnAccess.Core.Common;
using UnAccess.Core.Entities;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Core.Security;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Data.Services;

internal sealed class SetupService : ISetupService
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly IUtcClock _clock;
    private readonly IHospitalClock _hospitalClock;
    private readonly ICurrentUser _current;
    private readonly IAccessGuard _guard;
    private readonly AuditWriter _audit;

    public SetupService(
        IDbContextFactory<UnAccessDbContext> factory,
        IUtcClock clock,
        IHospitalClock hospitalClock,
        ICurrentUser current,
        IAccessGuard guard,
        AuditWriter audit)
    {
        _factory = factory;
        _clock = clock;
        _hospitalClock = hospitalClock;
        _current = current;
        _guard = guard;
        _audit = audit;
    }

    public async Task ApplySchemaAsync(CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
        await SecurityBootstrap.EnsureAsync(db, _clock.UtcNow, cancellationToken);
        var timeZone = await db.SystemSettings.AsNoTracking()
            .Where(item => item.Key == SettingKeys.TimeZone)
            .Select(item => item.Value)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(timeZone))
            _hospitalClock.ApplyTimeZone(timeZone);
    }

    public async Task SaveInstallationSettingsAsync(InstallationSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var hospital = TextRules.Name(settings.HospitalName, "Hastane adı");
        var zone = (settings.TimeZoneId ?? "").Trim();
        if (!TimeZoneLookup.TryFind(zone, out _))
            throw new UnAccessException("Saat dilimi bulunamadı.");

        var directory = (settings.BackupDirectory ?? "").Trim();
        if (settings.BackupEnabled)
            directory = BackupFiles.RequireDirectory(directory);
        else if (directory.Length > 0)
            directory = BackupFiles.RequireDirectory(directory);

        if (!TimeOnly.TryParse(settings.BackupTime, out var time))
            throw new UnAccessException("Yedekleme saati geçersiz.");
        if (settings.BackupRetentionDays < 1 || settings.BackupRetentionDays > 3650)
            throw new UnAccessException("Saklama süresi 1 ile 3650 gün arasında olmalıdır.");

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        if (await db.Users.AnyAsync(cancellationToken))
            _guard.Demand(Permissions.SettingsManage);

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SettingKeys.HospitalName] = hospital,
            [SettingKeys.TimeZone] = zone,
            [SettingKeys.BackupEnabled] = settings.BackupEnabled ? "true" : "false",
            [SettingKeys.BackupDirectory] = directory,
            [SettingKeys.BackupTime] = time.ToString("HH:mm"),
            [SettingKeys.BackupRetentionDays] = settings.BackupRetentionDays.ToString()
        };

        var now = _clock.UtcNow;
        foreach (var pair in values)
        {
            var row = await db.SystemSettings.FirstOrDefaultAsync(item => item.Key == pair.Key, cancellationToken);
            var previous = row?.Value;
            if (row == null)
            {
                db.SystemSettings.Add(new SystemSetting
                {
                    Key = pair.Key,
                    Value = pair.Value,
                    UpdatedAtUtc = now,
                    UpdatedByUserId = _current.UserId
                });
            }
            else if (row.Value == pair.Value)
            {
                continue;
            }
            else
            {
                row.Value = pair.Value;
                row.UpdatedAtUtc = now;
                row.UpdatedByUserId = _current.UserId;
            }

            var backup = pair.Key is SettingKeys.BackupEnabled or SettingKeys.BackupDirectory or SettingKeys.BackupTime or SettingKeys.BackupRetentionDays;
            _audit.Add(db, new AuditEntry
            {
                Action = backup ? AuditActions.BackupSettingsChanged : AuditActions.Updated,
                EntityType = AuditEntities.SystemSetting,
                EntityId = pair.Key,
                Description = backup ? $"Yedekleme ayarı güncellendi: {pair.Key}" : $"Kurulum ayarı güncellendi: {pair.Key}",
                OldValue = previous,
                NewValue = pair.Value
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        _hospitalClock.ApplyTimeZone(zone);
    }
}
