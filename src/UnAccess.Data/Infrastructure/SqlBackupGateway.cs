using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Backup;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;

namespace UnAccess.Data.Infrastructure;

internal sealed class SqlBackupGateway
{
    private readonly string _connectionString;
    private readonly string _databaseName;
    private readonly ILogger<SqlBackupGateway> _logger;

    public SqlBackupGateway(IConfiguration configuration, ILogger<SqlBackupGateway> logger)
    {
        var current = configuration.GetConnectionString("UnAccess");
        if (string.IsNullOrWhiteSpace(current))
            throw new UnAccessException(DataMessages.MissingConnectionString);
        _connectionString = current;
        _databaseName = SqlIdentifiers.DatabaseName(current);
        _logger = logger;
    }

    public string DatabaseName => _databaseName;

    public Task<bool> HasBackupPermissionAsync(CancellationToken cancellationToken) =>
        HasBackupPermissionAsync(_connectionString, cancellationToken);

    public async Task<bool> HasBackupPermissionAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'BACKUP DATABASE') = 1 THEN 1 ELSE 0 END;";
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is int value && value == 1;
    }

    public async Task<bool> HasRestorePermissionAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE
                WHEN IS_SRVROLEMEMBER('sysadmin') = 1 THEN 1
                WHEN IS_MEMBER('db_owner') = 1 THEN 1
                ELSE 0 END;
            """;
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is int value && value == 1;
    }

    public async Task BackupDatabaseAsync(string fullPath, CancellationToken cancellationToken) =>
        await BackupDatabaseAsync(_connectionString, _databaseName, fullPath, cancellationToken);

    public async Task BackupDatabaseAsync(string connectionString, string databaseName, string fullPath, CancellationToken cancellationToken)
    {
        var database = SqlIdentifiers.RequireDatabase(databaseName);
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 0;
            command.CommandText = """
                DECLARE @sql nvarchar(max) = N'BACKUP DATABASE ' + QUOTENAME(@database) + N' TO DISK = @disk WITH INIT, CHECKSUM';
                EXEC sp_executesql @sql, N'@disk nvarchar(4000)', @disk = @path;
                """;
            command.Parameters.Add(new SqlParameter("@database", System.Data.SqlDbType.NVarChar, 128) { Value = database });
            command.Parameters.Add(new SqlParameter("@path", System.Data.SqlDbType.NVarChar, 4000) { Value = fullPath });
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError("BACKUP DATABASE tamamlanamadı. {Detail}", BackupErrors.Technical(exception));
            throw new UnAccessException(BackupErrors.ForBackup(exception), exception);
        }
    }

    public async Task VerifyAsync(string fullPath, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(SqlIdentifiers.WithCatalog(_connectionString, "master"));
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 0;
            command.CommandText = "RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM;";
            command.Parameters.Add(new SqlParameter("@path", System.Data.SqlDbType.NVarChar, 4000) { Value = fullPath });
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("RESTORE VERIFYONLY başarısız. {Detail}", BackupErrors.Technical(exception));
            throw new UnAccessException(BackupMessages.VerifyFailed, exception);
        }
    }

    public async Task<string?> ReadSourceDatabaseAsync(string fullPath, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(SqlIdentifiers.WithCatalog(_connectionString, "master"));
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 0;
        command.CommandText = "RESTORE HEADERONLY FROM DISK = @path;";
        command.Parameters.Add(new SqlParameter("@path", System.Data.SqlDbType.NVarChar, 4000) { Value = fullPath });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        var ordinal = reader.GetOrdinal("DatabaseName");
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public async Task RestoreDatabaseAsync(string fullPath, CancellationToken cancellationToken)
    {
        var database = _databaseName;
        try
        {
            await using var connection = new SqlConnection(SqlIdentifiers.WithCatalog(_connectionString, "master"));
            await connection.OpenAsync(cancellationToken);
            await SetUserAccessAsync(connection, database, singleUser: true, cancellationToken);
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandTimeout = 0;
                command.CommandText = """
                    DECLARE @sql nvarchar(max) = N'RESTORE DATABASE ' + QUOTENAME(@database) + N' FROM DISK = @disk WITH REPLACE, RECOVERY, CHECKSUM';
                    EXEC sp_executesql @sql, N'@disk nvarchar(4000)', @disk = @path;
                    """;
                command.Parameters.Add(new SqlParameter("@database", System.Data.SqlDbType.NVarChar, 128) { Value = database });
                command.Parameters.Add(new SqlParameter("@path", System.Data.SqlDbType.NVarChar, 4000) { Value = fullPath });
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            finally
            {
                try
                {
                    await SetUserAccessAsync(connection, database, singleUser: false, cancellationToken);
                }
                catch (Exception exception)
                {
                    _logger.LogError("Veritabanı çok kullanıcılı moda alınamadı. {Detail}", BackupErrors.Technical(exception));
                }
            }
        }
        catch (Exception exception) when (exception is not UnAccessException)
        {
            _logger.LogError("RESTORE DATABASE tamamlanamadı. {Detail}", BackupErrors.Technical(exception));
            throw new UnAccessException(BackupErrors.ForRestore(exception), exception);
        }
        finally
        {
            SqlConnection.ClearAllPools();
        }
    }

    public async Task<PathProbeResult> ProbeDirectoryAsync(string directory, CancellationToken cancellationToken)
    {
        try
        {
            var seen = await DescribeAsync(directory, cancellationToken);
            if (seen.IsDirectory)
            {
                return new PathProbeResult
                {
                    SeenBySql = true,
                    Verified = false,
                    Message = BackupMessages.DirectoryUnverified
                };
            }

            return new PathProbeResult
            {
                SeenBySql = false,
                Verified = false,
                Message = seen.ParentExists
                    ? "SQL Server klasörü görmüyor. " + BackupMessages.DirectoryUnverified
                    : BackupMessages.DirectoryUnverified
            };
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Yedek klasörü SQL tarafından doğrulanamadı. {Detail}", BackupErrors.Technical(exception));
            return new PathProbeResult
            {
                SeenBySql = false,
                Verified = false,
                Message = BackupMessages.DirectoryUnverified
            };
        }
    }

    public async Task<bool> FileExistsAsync(string fullPath, CancellationToken cancellationToken)
    {
        try
        {
            var described = await DescribeAsync(fullPath, cancellationToken);
            return described.FileExists;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Yedek dosyası varlık kontrolü yapılamadı. {Detail}", BackupErrors.Technical(exception));
            return false;
        }
    }

    private async Task<FileDescription> DescribeAsync(string path, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(SqlIdentifiers.WithCatalog(_connectionString, "master"));
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE #fileexist ([File Exists] int, [File is a Directory] int, [Parent Directory Exists] int);
            INSERT #fileexist EXEC master.dbo.xp_fileexist @path;
            SELECT [File Exists], [File is a Directory], [Parent Directory Exists] FROM #fileexist;
            """;
        command.Parameters.Add(new SqlParameter("@path", System.Data.SqlDbType.NVarChar, 4000) { Value = path });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return new FileDescription(false, false, false);
        return new FileDescription(ReadFlag(reader, 0), ReadFlag(reader, 1), ReadFlag(reader, 2));
    }

    private static bool ReadFlag(SqlDataReader reader, int ordinal) =>
        !reader.IsDBNull(ordinal) && reader.GetInt32(ordinal) == 1;

    private static async Task SetUserAccessAsync(SqlConnection connection, string database, bool singleUser, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 0;
        command.CommandText = singleUser
            ? "DECLARE @sql nvarchar(max) = N'ALTER DATABASE ' + QUOTENAME(@database) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE'; EXEC sp_executesql @sql;"
            : "DECLARE @sql nvarchar(max) = N'ALTER DATABASE ' + QUOTENAME(@database) + N' SET MULTI_USER'; EXEC sp_executesql @sql;";
        command.Parameters.Add(new SqlParameter("@database", System.Data.SqlDbType.NVarChar, 128) { Value = database });
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private readonly record struct FileDescription(bool FileExists, bool IsDirectory, bool ParentExists);
}
