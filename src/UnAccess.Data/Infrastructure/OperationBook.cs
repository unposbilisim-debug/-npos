using Microsoft.EntityFrameworkCore;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Backup;
using UnAccess.Core.Common;
using UnAccess.Core.Entities;

namespace UnAccess.Data.Infrastructure;

internal sealed class OperationBook
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly IUtcClock _clock;

    public OperationBook(IDbContextFactory<UnAccessDbContext> factory, IUtcClock clock)
    {
        _factory = factory;
        _clock = clock;
    }

    public async Task AbandonRunningAsync(bool onlyStale, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var now = _clock.UtcNow;
        var cutoff = now.AddHours(-3);
        var backups = await db.BackupHistories.Where(item => item.Status == BackupStatuses.Running).ToListAsync(cancellationToken);
        var restores = await db.RestoreHistories.Where(item => item.Status == BackupStatuses.Running).ToListAsync(cancellationToken);
        foreach (var row in backups)
        {
            if (onlyStale && row.StartedAtUtc >= cutoff)
                continue;
            row.Status = BackupStatuses.Failed;
            row.CompletedAtUtc = now;
            row.ErrorMessage = Trim(BackupMessages.Abandoned);
        }

        foreach (var row in restores)
        {
            if (onlyStale && row.StartedAtUtc >= cutoff)
                continue;
            row.Status = BackupStatuses.Failed;
            row.CompletedAtUtc = now;
            row.ErrorMessage = Trim(BackupMessages.Abandoned);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> StartBackupAsync(BackupHistory row, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        db.BackupHistories.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    public async Task CompleteBackupAsync(int id, long? fileSize, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var row = await db.BackupHistories.FirstAsync(item => item.Id == id, cancellationToken);
        row.Status = BackupStatuses.Completed;
        row.CompletedAtUtc = _clock.UtcNow;
        row.FileSizeBytes = fileSize;
        row.ErrorMessage = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FailBackupAsync(int id, string message, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var row = await db.BackupHistories.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row == null)
            return;
        row.Status = BackupStatuses.Failed;
        row.CompletedAtUtc = _clock.UtcNow;
        row.ErrorMessage = Trim(message);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> StartRestoreAsync(RestoreHistory row, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        db.RestoreHistories.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    public async Task FinishRestoreAsync(int? id, string status, string? safetyFile, string? error, int? userId, string backupFileName, DateTime startedAtUtc, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var row = id is int existing
            ? await db.RestoreHistories.FirstOrDefaultAsync(item => item.Id == existing, cancellationToken)
            : null;
        if (row == null)
        {
            row = new RestoreHistory
            {
                BackupFileName = TrimFile(backupFileName),
                StartedAtUtc = startedAtUtc,
                TriggeredByUserId = userId
            };
            db.RestoreHistories.Add(row);
        }

        row.Status = status;
        row.CompletedAtUtc = _clock.UtcNow;
        row.SafetyBackupFileName = string.IsNullOrWhiteSpace(safetyFile) ? null : TrimFile(safetyFile);
        row.ErrorMessage = string.IsNullOrWhiteSpace(error) ? null : Trim(error);
        await db.SaveChangesAsync(cancellationToken);
    }

    public static long? TrySize(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? info.Length : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Trim(string message)
    {
        var text = message.Trim();
        return text.Length <= FieldLengths.ErrorMessage ? text : text[..FieldLengths.ErrorMessage];
    }

    private static string TrimFile(string name)
    {
        var text = name.Trim();
        return text.Length <= FieldLengths.BackupFileName ? text : text[..FieldLengths.BackupFileName];
    }
}
