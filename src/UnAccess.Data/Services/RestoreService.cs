using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Audit;
using UnAccess.Core.Backup;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Core.Security;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Data.Services;

internal sealed class RestoreService : IRestoreService
{
    private readonly IMaintenanceGate _gate;
    private readonly SqlBackupGateway _gateway;
    private readonly ISafetyBackup _safety;
    private readonly OperationBook _book;
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly ICurrentUser _current;
    private readonly IAccessGuard _guard;
    private readonly IUtcClock _clock;
    private readonly AuditWriter _audit;
    private readonly ILogger<RestoreService> _logger;

    public RestoreService(
        IMaintenanceGate gate,
        SqlBackupGateway gateway,
        ISafetyBackup safety,
        OperationBook book,
        IDbContextFactory<UnAccessDbContext> factory,
        ICurrentUser current,
        IAccessGuard guard,
        IUtcClock clock,
        AuditWriter audit,
        ILogger<RestoreService> logger)
    {
        _gate = gate;
        _gateway = gateway;
        _safety = safety;
        _book = book;
        _factory = factory;
        _current = current;
        _guard = guard;
        _clock = clock;
        _audit = audit;
        _logger = logger;
    }

    public async Task<BackupInspection> InspectAsync(string backupFile, CancellationToken cancellationToken)
    {
        _guard.Demand(Permissions.BackupManage);
        if (string.IsNullOrWhiteSpace(backupFile))
            return Failed(BackupMessages.VerifyFailed, false);

        var readable = false;
        try
        {
            readable = File.Exists(backupFile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            readable = false;
        }

        var seen = false;
        try
        {
            seen = await _gateway.FileExistsAsync(backupFile, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Yedek dosyası SQL tarafından görülemedi. {Detail}", BackupErrors.Technical(exception));
        }

        if (!readable && !seen)
            return Failed("Yedek dosyası bulunamadı.", await SafeRestorePermissionAsync(cancellationToken));

        var restorePermission = await SafeRestorePermissionAsync(cancellationToken);
        var backupPermission = false;
        try
        {
            backupPermission = await _gateway.HasBackupPermissionAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Yedekleme yetkisi okunamadı. {Detail}", BackupErrors.Technical(exception));
        }

        if (!restorePermission)
            return Failed(BackupMessages.RestorePermissionDenied, false);

        try
        {
            await _gateway.VerifyAsync(backupFile, cancellationToken);
            var source = await _gateway.ReadSourceDatabaseAsync(backupFile, cancellationToken);
            if (string.IsNullOrWhiteSpace(source))
                return Failed(BackupMessages.VerifyFailed, true);
            return new BackupInspection
            {
                CanRead = true,
                Message = "Yedek doğrulandı.",
                SourceDatabase = source,
                HasBackupPermission = backupPermission,
                HasRestorePermission = true
            };
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Yedek doğrulaması başarısız. {Detail}", BackupErrors.Technical(exception));
            return Failed(BackupMessages.VerifyFailed, true);
        }
    }

    public async Task<RestoreResult> RestoreAsync(RestoreRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireConfirmation(request);
        var inspection = await InspectAsync(request.BackupFile, cancellationToken);
        if (!inspection.HasRestorePermission)
            throw new UnAccessException(BackupMessages.RestorePermissionDenied);
        if (!inspection.CanRead)
            throw new UnAccessException(string.IsNullOrWhiteSpace(inspection.Message) ? BackupMessages.VerifyFailed : inspection.Message);
        return await ExecuteCoreAsync(request, cancellationToken);
    }

    internal Task<RestoreResult> ExecuteAsync(RestoreRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireConfirmation(request);
        _guard.Demand(Permissions.BackupManage);
        return ExecuteCoreAsync(request, cancellationToken);
    }

    private async Task<RestoreResult> ExecuteCoreAsync(RestoreRequest request, CancellationToken cancellationToken)
    {
        if (!await _gateway.HasRestorePermissionAsync(cancellationToken))
            throw new UnAccessException(BackupMessages.RestorePermissionDenied);

        var lease = await _gate.TryEnterExclusiveAsync(cancellationToken);
        if (lease == null)
            throw new UnAccessException(BackupMessages.Busy);

        await using var _ = lease;
        await _book.AbandonRunningAsync(onlyStale: false, cancellationToken);
        var started = _clock.UtcNow;
        var fileName = Path.GetFileName(request.BackupFile.Trim());
        await AuditAsync(AuditActions.RestoreRequested, "Geri yükleme istendi.", fileName, cancellationToken);
        var historyId = await _book.StartRestoreAsync(new Core.Entities.RestoreHistory
        {
            BackupFileName = fileName,
            StartedAtUtc = started,
            Status = BackupStatuses.Running,
            TriggeredByUserId = _current.UserId
        }, cancellationToken);

        BackupResult safety;
        try
        {
            safety = await _safety.CreateSafetyAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            await FailAsync(historyId, fileName, started, null, exception, cancellationToken);
            throw new UnAccessException(BackupMessages.SafetyFailed, exception);
        }

        if (!safety.Succeeded || string.IsNullOrWhiteSpace(safety.FullPath))
        {
            await FailAsync(historyId, fileName, started, safety.FileName, null, cancellationToken);
            throw new UnAccessException(BackupMessages.SafetyFailed);
        }

        _logger.LogWarning("Geri yükleme başladı. Dosya {FileName}. Güvenlik yedeği {SafetyFile}.", fileName, safety.FileName);
        try
        {
            await _gateway.RestoreDatabaseAsync(request.BackupFile, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError("Geri yükleme başarısız. Dosya {FileName}. {Detail}", fileName, BackupErrors.Technical(exception));
            await TryRecoverSafetyAsync(safety.FullPath, cancellationToken);
            await FailAsync(historyId, fileName, started, safety.FileName, exception, cancellationToken);
            _logger.LogWarning("Geri yükleme sonucu {Status}. Dosya {FileName}.", BackupStatuses.Failed, fileName);
            throw new UnAccessException(BackupMessages.RestoreFailed, exception);
        }

        await _book.FinishRestoreAsync(historyId, BackupStatuses.Completed, safety.FileName, null, _current.UserId, fileName, started, cancellationToken);
        await AuditAsync(AuditActions.RestoreCompleted, "Geri yükleme tamamlandı.", fileName, cancellationToken);
        _logger.LogWarning("Geri yükleme sonucu {Status}. Dosya {FileName}.", BackupStatuses.Completed, fileName);
        return new RestoreResult
        {
            Succeeded = true,
            Message = "Veritabanı yedeği geri yüklendi.",
            SafetyBackupFileName = safety.FileName
        };
    }

    private async Task TryRecoverSafetyAsync(string safetyPath, CancellationToken cancellationToken)
    {
        try
        {
            await _gateway.RestoreDatabaseAsync(safetyPath, cancellationToken);
            _logger.LogWarning("Başarısız geri yüklemenin ardından güvenlik yedeği yeniden uygulandı.");
        }
        catch (Exception exception)
        {
            _logger.LogError("Güvenlik yedeği yeniden uygulanamadı. {Detail}", BackupErrors.Technical(exception));
        }
    }

    private async Task FailAsync(int historyId, string fileName, DateTime started, string? safetyFile, Exception? exception, CancellationToken cancellationToken)
    {
        var detail = exception == null ? BackupMessages.SafetyFailed : BackupErrors.ForRestore(exception);
        if (exception != null)
            _logger.LogError("Geri yükleme kaydı başarısız. {Detail}", BackupErrors.Technical(exception));
        try
        {
            await _book.FinishRestoreAsync(historyId, BackupStatuses.Failed, safetyFile, detail, _current.UserId, fileName, started, cancellationToken);
            await AuditAsync(AuditActions.RestoreFailed, "Geri yükleme başarısız.", fileName, cancellationToken);
        }
        catch (Exception writeError)
        {
            _logger.LogError("Geri yükleme geçmişi yazılamadı. {Detail}", BackupErrors.Technical(writeError));
        }
    }

    private async Task AuditAsync(string action, string description, string fileName, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            _audit.Add(db, new AuditEntry
            {
                Action = action,
                EntityType = AuditEntities.Restore,
                EntityId = fileName,
                Description = description
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError("Geri yükleme denetim kaydı yazılamadı. {Detail}", BackupErrors.Technical(exception));
        }
    }

    private async Task<bool> SafeRestorePermissionAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _gateway.HasRestorePermissionAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Geri yükleme yetkisi okunamadı. {Detail}", BackupErrors.Technical(exception));
            return false;
        }
    }

    private void RequireConfirmation(RestoreRequest request)
    {
        if (!string.Equals(request.ConfirmationText, BackupMessages.ConfirmationText, StringComparison.Ordinal))
            throw new UnAccessException(BackupMessages.ConfirmationRejected);
    }

    private static BackupInspection Failed(string message, bool restorePermission) =>
        new()
        {
            CanRead = false,
            Message = message,
            HasRestorePermission = restorePermission
        };
}
