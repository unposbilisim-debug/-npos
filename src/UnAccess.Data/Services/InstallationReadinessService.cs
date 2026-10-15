using Microsoft.EntityFrameworkCore;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Backup;
using UnAccess.Core.Models;
using UnAccess.Core.Security;
using UnAccess.Core.Sync;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Data.Services;

internal sealed class InstallationReadinessService : IInstallationReadinessService
{
    private readonly IDatabaseHealthService _health;
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly IRuntimeSettings _settings;
    private readonly IHospitalClock _hospitalClock;
    private readonly IUtcClock _clock;
    private readonly IUnAccessEnvironment _environment;
    private readonly IConnectionConfigurationStore _store;
    private readonly IConfigurationAccessor _configuration;
    private readonly SqlBackupGateway _gateway;

    public InstallationReadinessService(
        IDatabaseHealthService health,
        IDbContextFactory<UnAccessDbContext> factory,
        IRuntimeSettings settings,
        IHospitalClock hospitalClock,
        IUtcClock clock,
        IUnAccessEnvironment environment,
        IConnectionConfigurationStore store,
        IConfigurationAccessor configuration,
        SqlBackupGateway gateway)
    {
        _health = health;
        _factory = factory;
        _settings = settings;
        _hospitalClock = hospitalClock;
        _clock = clock;
        _environment = environment;
        _store = store;
        _configuration = configuration;
        _gateway = gateway;
    }

    public async Task<ReadinessReport> EvaluateAsync(CancellationToken cancellationToken)
    {
        var items = new List<ReadinessItem>();
        var health = await _health.GetAsync(cancellationToken);
        items.Add(new ReadinessItem
        {
            Name = "SQL bağlantısı",
            Level = health.Connected ? ReadinessLevels.Ok : ReadinessLevels.Error,
            Detail = health.ConnectionText
        });
        items.Add(new ReadinessItem
        {
            Name = "Migration",
            Level = !health.Connected || health.PendingMigrations ? ReadinessLevels.Error : ReadinessLevels.Ok,
            Detail = health.MigrationText
        });

        var hasAdmin = false;
        if (health.Connected && health.TablesReadable)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            hasAdmin = await db.Users.AsNoTracking().AnyAsync(cancellationToken);
        }

        items.Add(new ReadinessItem
        {
            Name = "İlk yönetici",
            Level = hasAdmin ? ReadinessLevels.Ok : ReadinessLevels.Error,
            Detail = hasAdmin ? "Yönetici hesabı var" : "İlk yönetici oluşturulmamış"
        });

        var hospital = health.TablesReadable ? await _settings.HospitalNameAsync(cancellationToken) : "";
        items.Add(new ReadinessItem
        {
            Name = "Hastane adı",
            Level = string.IsNullOrWhiteSpace(hospital) ? ReadinessLevels.Error : ReadinessLevels.Ok,
            Detail = string.IsNullOrWhiteSpace(hospital) ? "Hastane adı boş" : hospital
        });

        var zoneOk = false;
        var zone = health.TablesReadable ? await _settings.TimeZoneIdAsync(cancellationToken) : "";
        if (!string.IsNullOrWhiteSpace(zone) && TimeZoneLookup.TryFind(zone, out _))
            zoneOk = true;

        items.Add(new ReadinessItem
        {
            Name = "Saat dilimi",
            Level = zoneOk ? ReadinessLevels.Ok : ReadinessLevels.Error,
            Detail = zoneOk ? zone : "Saat dilimi geçersiz"
        });

        var enabled = health.TablesReadable && await _settings.BackupEnabledAsync(cancellationToken);
        var directory = health.TablesReadable ? await _settings.BackupDirectoryAsync(cancellationToken) : "";
        var backupLevel = !enabled
            ? ReadinessLevels.Warning
            : string.IsNullOrWhiteSpace(directory) ? ReadinessLevels.Error : ReadinessLevels.Ok;
        var backupDetail = !enabled
            ? "Otomatik yedekleme kapalı"
            : string.IsNullOrWhiteSpace(directory) ? BackupMessages.DirectoryMissing : directory;
        items.Add(new ReadinessItem { Name = "Yedekleme ayarı", Level = backupLevel, Detail = backupDetail });

        var permission = false;
        var permissionKnown = false;
        if (health.Connected)
        {
            try
            {
                permission = await _gateway.HasBackupPermissionAsync(cancellationToken);
                permissionKnown = true;
            }
            catch (Exception)
            {
                permissionKnown = false;
            }
        }

        var permissionLevel = !permissionKnown
            ? ReadinessLevels.Warning
            : permission ? ReadinessLevels.Ok : enabled ? ReadinessLevels.Error : ReadinessLevels.Warning;
        items.Add(new ReadinessItem
        {
            Name = "Yedekleme yetkisi",
            Level = permissionLevel,
            Detail = !permissionKnown ? "Bilinmiyor" : permission ? "BACKUP DATABASE yetkisi var" : BackupMessages.PermissionDenied
        });

        var protectedRead = _store.Read();
        var environmentConnection = !string.IsNullOrWhiteSpace(_configuration.EnvironmentConnectionString);
        string configLevel;
        string configDetail;
        if (protectedRead.Success)
        {
            configLevel = ReadinessLevels.Ok;
            configDetail = "Korunan bağlantı ayarı okunabildi";
        }
        else if (environmentConnection && !_environment.IsProduction)
        {
            configLevel = ReadinessLevels.Warning;
            configDetail = "Geliştirme bağlantısı ortam değişkeninden okunuyor";
        }
        else if (environmentConnection)
        {
            configLevel = ReadinessLevels.Warning;
            configDetail = "Bağlantı ortam değişkeninden okunuyor. Korunan dosya yok.";
        }
        else if (_environment.IsProduction)
        {
            configLevel = ReadinessLevels.Error;
            configDetail = protectedRead.Error ?? "Korunan bağlantı ayarı yok";
        }
        else
        {
            configLevel = ReadinessLevels.Warning;
            configDetail = "Korunan bağlantı ayarı yok";
        }

        items.Add(new ReadinessItem { Name = "Bağlantı yapılandırması", Level = configLevel, Detail = configDetail });

        var alive = false;
        if (health.TablesReadable)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var stale = await _settings.HeartbeatStaleAsync(cancellationToken);
            var last = await db.ServiceHeartbeats.AsNoTracking()
                .Where(item => item.ServiceName == ServiceNames.DeviceSync)
                .OrderByDescending(item => item.LastSeenAtUtc)
                .Select(item => (DateTime?)item.LastSeenAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            alive = last is DateTime seen && HeartbeatStatus.IsAlive(seen, _clock.UtcNow, stale);
        }

        items.Add(new ReadinessItem
        {
            Name = "DeviceService",
            Level = alive ? ReadinessLevels.Ok : ReadinessLevels.Warning,
            Detail = alive ? "Heartbeat alındı" : "Heartbeat yok. Servis daha sonra kurulabilir."
        });

        return new ReadinessReport { Items = items };
    }
}

internal interface IConfigurationAccessor
{
    string? EnvironmentConnectionString { get; }
}
