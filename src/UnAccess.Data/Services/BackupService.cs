using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Audit;
using UnAccess.Core.Backup;
using UnAccess.Core.Entities;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Core.Security;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Data.Services;

internal interface ISafetyBackup
{
    Task<BackupResult> CreateSafetyAsync(CancellationToken cancellationToken);
}

internal sealed class BackupService : IBackupService, ISafetyBackup
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly IRuntimeSettings _settings;
    private readonly IHospitalClock _hospitalClock;
    private readonly IUtcClock _clock;
    private readonly ICurrentUser _current;
    private readonly IAccessGuard _guard;
    private readonly IMaintenanceGate _gate;
    private readonly SqlBackupGateway _gateway;
    private readonly OperationBook _book;
    private readonly AuditWriter _audit;
    private readonly ILogger<BackupService> _logger;
    private readonly string _dataSource;

    public BackupService(
        IDbContextFactory<UnAccessDbContext> factory,
        IRuntimeSettings settings,
        IHospitalClock hospitalClock,
        IUtcClock clock,
        ICurrentUser current,
        IAccessGuard guard,
        IMaintenanceGate gate,
        SqlBackupGateway gateway,
        OperationBook book,
        AuditWriter audit,
        IConfiguration configuration,
        ILogger<BackupService> logger)
    {
        _factory = factory;
        _settings = settings;
        _hospitalClock = hospitalClock;
        _clock = clock;
        _current = current;
        _guard = guard;
        _gateway = gateway;
        _gate = gate;
        _book = book;
        _audit = audit;
        _logger = logger;
        var connection = configuration.GetConnectionString("UnAccess");
        _dataSource = string.IsNullOrWhiteSpace(connection) ? "" : SqlIdentifiers.DataSource(connection);
    }

    public Task<BackupResult> CreateManualAsync(CancellationToken cancellationToken) =>
        RunAsync(BackupKinds.Manual, BackupTriggers.User, demand: true, takeLock: true, enforceSchedule: false, cancellationToken);

    public Task<BackupResult> CreateScheduledAsync(CancellationToken cancellationToken) =>
        RunAsync(BackupKinds.Scheduled, BackupTriggers.DeviceService, demand: false, takeLock: true, enforceSchedule: true, cancellationToken);

    public Task<BackupResult> CreateSafetyAsync(CancellationToken cancellationToken) =>
        RunAsync(BackupKinds.PreRestoreSafety, BackupTriggers.User, demand: false, takeLock: false, enforceSchedule: false, cancellationToken);

    public async Task<BackupStatusView> GetStatusAsync(CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.SystemView);
        var enabled = await _settings.BackupEnabledAsync(cancellationToken);
        var directory = await _settings.BackupDirectoryAsync(cancellationToken);
        var time = await _settings.BackupTimeAsync(cancellationToken);
        var retention = await _settings.BackupRetentionDaysAsync(cancellationToken);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var last = await db.BackupHistories.AsNoTracking()
            .Where(item => item.Status == BackupStatuses.Completed)
            .OrderByDescending(item => item.CompletedAtUtc)
            .Select(item => new { item.FileName, item.CompletedAtUtc })
            .FirstOrDefaultAsync(cancellationToken);

        var status = !enabled
            ? "Kapalı"
            : string.IsNullOrWhiteSpace(directory)
                ? "Klasör tanımlı değil"
                : "Açık";
        var lastText = "—";
        if (last?.CompletedAtUtc is DateTime completed)
            lastText = _hospitalClock.ToHospitalLocal(DateTime.SpecifyKind(completed, DateTimeKind.Utc)).ToString("dd.MM.yyyy HH:mm:ss");

        return new BackupStatusView
        {
            Enabled = enabled,
            Directory = directory,
            TimeText = time.ToString("HH:mm"),
            RetentionDays = retention,
            StatusText = status,
            LastSuccessText = lastText,
            LastFileName = string.IsNullOrWhiteSpace(last?.FileName) ? "—" : last.FileName,
            FolderText = BackupDisk.FolderAccess(_dataSource, directory)
        };
    }

    public async Task ApplyRetentionAsync(CancellationToken cancellationToken)
    {
        var directory = await _settings.BackupDirectoryAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(directory))
            return;

        var days = await _settings.BackupRetentionDaysAsync(cancellationToken);
        var now = _clock.UtcNow;
        await using var lease = await _gate.TryEnterExclusiveAsync(cancellationToken);
        if (lease == null)
        {
            _logger.LogInformation(BackupMessages.Busy);
            return;
        }

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.BackupHistories.AsNoTracking()
            .Where(item => item.BackupType == BackupKinds.Scheduled && item.Status == BackupStatuses.Completed && item.CompletedAtUtc != null)
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            var candidate = new BackupRetentionCandidate
            {
                BackupType = row.BackupType,
                Status = row.Status,
                FileName = row.FileName,
                BackupDirectory = row.BackupDirectory,
                CompletedAtUtc = row.CompletedAtUtc
            };
            if (!BackupRetention.ShouldDelete(candidate, directory, now, days))
                continue;

            var path = BackupFiles.ResolveLocalDeletePath(directory, row.FileName);
            if (path == null)
                continue;

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception exception)
            {
                _logger.LogWarning("Eski yedek dosyası silinemedi. {File} {Detail}", row.FileName, BackupErrors.Technical(exception));
            }
        }
    }

    public Task RecoverStaleAsync(CancellationToken cancellationToken) =>
        _book.AbandonRunningAsync(onlyStale: true, cancellationToken);

    public Task<PathProbeResult> ProbeDirectoryAsync(string directory, CancellationToken cancellationToken)
    {
        try
        {
            BackupFiles.RequireDirectory(directory);
        }
        catch (UnAccessException exception)
        {
            return Task.FromResult(new PathProbeResult { SeenBySql = false, Verified = false, Message = exception.UserMessage });
        }

        return _gateway.ProbeDirectoryAsync(directory, cancellationToken);
    }

    private async Task<BackupResult> RunAsync(string kind, string trigger, bool demand, bool takeLock, bool enforceSchedule, CancellationToken cancellationToken)
    {
        if (demand)
            _guard.Demand(Permissions.BackupManage);

        var directory = await _settings.BackupDirectoryAsync(cancellationToken);
        try
        {
            directory = BackupFiles.RequireDirectory(directory);
        }
        catch (UnAccessException exception)
        {
            if (!demand)
                return new BackupResult { Succeeded = false, Message = exception.UserMessage };
            throw;
        }

        if (enforceSchedule && !await DueAsync(cancellationToken))
            return new BackupResult { Succeeded = true, Message = "Planlı yedek bugün için gerekmiyor." };

        IAsyncDisposable? lease = null;
        if (takeLock)
        {
            lease = await _gate.TryEnterExclusiveAsync(cancellationToken);
            if (lease == null)
            {
                if (demand)
                    throw new UnAccessException(BackupMessages.Busy);
                return new BackupResult { Succeeded = false, Message = BackupMessages.Busy };
            }
        }

        await using var _ = lease;
        if (takeLock)
            await _book.AbandonRunningAsync(onlyStale: false, cancellationToken);
        if (enforceSchedule && !await DueAsync(cancellationToken))
            return new BackupResult { Succeeded = true, Message = "Planlı yedek bugün için gerekmiyor." };

        if (!await _gateway.HasBackupPermissionAsync(cancellationToken))
        {
            await RecordDeniedAsync(kind, trigger, directory, cancellationToken);
            if (demand)
                throw new UnAccessException(BackupMessages.PermissionDenied);
            return new BackupResult { Succeeded = false, Message = BackupMessages.PermissionDenied };
        }

        var local = _hospitalClock.ToHospitalLocal(_clock.UtcNow);
        var fileName = await AllocateNameAsync(directory, BackupFiles.Create(_gateway.DatabaseName, local, kind), cancellationToken);
        var fullPath = BackupFiles.Join(directory, fileName);
        if (demand)
            await AuditRequestedAsync(fileName, cancellationToken);

        var historyId = await _book.StartBackupAsync(new BackupHistory
        {
            BackupType = kind,
            FileName = fileName,
            BackupDirectory = directory,
            StartedAtUtc = _clock.UtcNow,
            Status = BackupStatuses.Running,
            TriggeredBy = trigger,
            TriggeredByUserId = trigger == BackupTriggers.User ? _current.UserId : null
        }, cancellationToken);

        try
        {
            await _gateway.BackupDatabaseAsync(fullPath, cancellationToken);
            await _book.CompleteBackupAsync(historyId, OperationBook.TrySize(fullPath), cancellationToken);
            _logger.LogInformation("Veritabanı yedeği oluşturuldu. Dosya {FileName} Tür {Kind}", fileName, kind);
            return new BackupResult
            {
                Succeeded = true,
                Message = BackupMessages.Succeeded,
                FileName = fileName,
                FullPath = fullPath
            };
        }
        catch (Exception exception)
        {
            var message = exception is UnAccessException known ? known.UserMessage : BackupErrors.ForBackup(exception);
            await _book.FailBackupAsync(historyId, message, cancellationToken);
            _logger.LogError("Yedekleme başarısız. Dosya {FileName} {Detail}", fileName, BackupErrors.Technical(exception));
            if (demand)
                throw new UnAccessException(message, exception);
            return new BackupResult { Succeeded = false, Message = message, FileName = fileName, FullPath = fullPath };
        }
    }

    private async Task<bool> DueAsync(CancellationToken cancellationToken)
    {
        if (!await _settings.BackupEnabledAsync(cancellationToken))
            return false;

        var planned = await _settings.BackupTimeAsync(cancellationToken);
        var hospitalNow = _hospitalClock.ToHospitalLocal(_clock.UtcNow);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var lastUtc = await db.BackupHistories.AsNoTracking()
            .Where(item => item.BackupType == BackupKinds.Scheduled && item.Status == BackupStatuses.Completed && item.CompletedAtUtc != null)
            .OrderByDescending(item => item.CompletedAtUtc)
            .Select(item => item.CompletedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        DateOnly? lastDay = lastUtc is DateTime stamp
            ? DateOnly.FromDateTime(_hospitalClock.ToHospitalLocal(DateTime.SpecifyKind(stamp, DateTimeKind.Utc)))
            : null;
        return BackupSchedule.ShouldRun(true, planned, hospitalNow, lastDay);
    }

    private async Task<string> AllocateNameAsync(string directory, string fileName, CancellationToken cancellationToken)
    {
        var candidate = fileName;
        for (var suffix = 2; suffix < 100; suffix++)
        {
            var fullPath = BackupFiles.Join(directory, candidate);
            var remote = await _gateway.FileExistsAsync(fullPath, cancellationToken);
            var local = false;
            try
            {
                local = File.Exists(fullPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                local = false;
            }

            if (!remote && !local)
                return candidate;
            var stem = Path.GetFileNameWithoutExtension(fileName);
            candidate = stem + "_" + suffix.ToString() + ".bak";
        }

        throw new UnAccessException(BackupMessages.DirectoryInvalid);
    }

    private async Task RecordDeniedAsync(string kind, string trigger, string directory, CancellationToken cancellationToken)
    {
        var local = _hospitalClock.ToHospitalLocal(_clock.UtcNow);
        await _book.StartBackupAsync(new BackupHistory
        {
            BackupType = kind,
            FileName = BackupFiles.Create(_gateway.DatabaseName, local, kind),
            BackupDirectory = directory,
            StartedAtUtc = _clock.UtcNow,
            CompletedAtUtc = _clock.UtcNow,
            Status = BackupStatuses.Failed,
            TriggeredBy = trigger,
            TriggeredByUserId = trigger == BackupTriggers.User ? _current.UserId : null,
            ErrorMessage = BackupMessages.PermissionDenied
        }, cancellationToken);
        _logger.LogWarning(BackupMessages.PermissionDenied);
    }

    private async Task AuditRequestedAsync(string fileName, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        _audit.Add(db, new AuditEntry
        {
            Action = AuditActions.ManualBackupRequested,
            EntityType = AuditEntities.Backup,
            EntityId = fileName,
            Description = "Manuel veritabanı yedeği istendi."
        });
        await db.SaveChangesAsync(cancellationToken);
    }

}
