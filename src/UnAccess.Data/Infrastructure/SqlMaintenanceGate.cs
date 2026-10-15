using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Security;

namespace UnAccess.Data.Infrastructure;

internal sealed class SqlMaintenanceGate : IMaintenanceGate
{
    private readonly string _connectionString;
    private readonly string _resource;
    private readonly ILogger<SqlMaintenanceGate> _logger;

    public SqlMaintenanceGate(IConfiguration configuration, ILogger<SqlMaintenanceGate> logger)
    {
        var current = configuration.GetConnectionString("UnAccess");
        if (string.IsNullOrWhiteSpace(current))
            throw new UnAccessException(DataMessages.MissingConnectionString);
        _connectionString = SqlIdentifiers.WithCatalog(current, "master");
        _resource = "UnAccess.Maintenance:" + SqlIdentifiers.DatabaseName(current);
        _logger = logger;
    }

    public Task<IAsyncDisposable?> TryEnterExclusiveAsync(CancellationToken cancellationToken) =>
        TryEnterAsync("Exclusive", cancellationToken);

    public Task<IAsyncDisposable?> TryEnterSharedAsync(CancellationToken cancellationToken) =>
        TryEnterAsync("Shared", cancellationToken);

    private async Task<IAsyncDisposable?> TryEnterAsync(string mode, CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @result int;
                EXEC @result = sp_getapplock
                    @Resource = @resource,
                    @LockMode = @mode,
                    @LockOwner = N'Session',
                    @LockTimeout = 0;
                SELECT @result;
                """;
            command.Parameters.Add(new SqlParameter("@resource", System.Data.SqlDbType.NVarChar, 255) { Value = _resource });
            command.Parameters.Add(new SqlParameter("@mode", System.Data.SqlDbType.NVarChar, 32) { Value = mode });
            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            var result = scalar is int code ? code : -999;
            if (result < 0)
            {
                await connection.DisposeAsync();
                return null;
            }

            return new HeldLock(connection, _resource, _logger);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class HeldLock : IAsyncDisposable
    {
        private readonly SqlConnection _connection;
        private readonly string _resource;
        private readonly ILogger _logger;
        private int _released;

        public HeldLock(SqlConnection connection, string resource, ILogger logger)
        {
            _connection = connection;
            _resource = resource;
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
                return;

            try
            {
                if (_connection.State == System.Data.ConnectionState.Open)
                {
                    await using var command = _connection.CreateCommand();
                    command.CommandText = "EXEC sp_releaseapplock @Resource = @resource, @LockOwner = N'Session';";
                    command.Parameters.Add(new SqlParameter("@resource", System.Data.SqlDbType.NVarChar, 255) { Value = _resource });
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning("Bakım kilidi bırakılırken hata oluştu. {Detail}", SensitiveDataSanitizer.Clean(exception.ToString()));
            }

            await _connection.DisposeAsync();
        }
    }
}
