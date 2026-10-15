using Microsoft.Data.SqlClient;
using UnAccess.Core.Backup;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Security;

namespace UnAccess.Data.Infrastructure;

internal static class BackupErrors
{
    public static string ForBackup(Exception exception)
    {
        if (exception is UnAccessException known)
            return known.UserMessage;

        var sql = Find(exception);
        if (sql is { Number: 262 or 15247 })
            return BackupMessages.PermissionDenied;
        if (sql != null)
            return BackupMessages.BackupPathFailed;
        return "Yedekleme tamamlanamadı. Ayrıntı sistem günlüğüne yazıldı.";
    }

    public static string ForRestore(Exception exception)
    {
        if (exception is UnAccessException known)
            return known.UserMessage;

        var sql = Find(exception);
        if (sql is { Number: 262 or 15247 })
            return BackupMessages.RestorePermissionDenied;
        return BackupMessages.RestoreFailed;
    }

    public static string Technical(Exception exception) =>
        SensitiveDataSanitizer.Clean(exception.ToString());

    private static SqlException? Find(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is SqlException sql)
                return sql;
        }

        return null;
    }
}
