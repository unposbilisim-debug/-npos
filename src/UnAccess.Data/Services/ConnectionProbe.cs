using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Core.Security;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Data.Services;

internal sealed class ConnectionProbe : IConnectionProbe
{
    private readonly ILogger<ConnectionProbe> _logger;

    public ConnectionProbe(ILogger<ConnectionProbe> logger)
    {
        _logger = logger;
    }

    public string BuildConnectionString(SqlEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var server = (endpoint.Server ?? "").Trim();
        var instance = (endpoint.Instance ?? "").Trim();
        var database = SqlIdentifiers.RequireDatabase(endpoint.Database);
        if (server.Length == 0 || server.Any(char.IsControl) || instance.Any(char.IsControl))
            throw new UnAccessException("Sunucu adı geçersiz.");

        var source = instance.Length == 0 ? server : server + "\\" + instance;
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = source,
            InitialCatalog = database,
            IntegratedSecurity = endpoint.IntegratedSecurity,
            TrustServerCertificate = true,
            Encrypt = false,
            ConnectTimeout = 8
        };
        if (!endpoint.IntegratedSecurity)
        {
            var user = (endpoint.UserName ?? "").Trim();
            if (user.Length == 0)
                throw new UnAccessException("SQL kullanıcı adı zorunludur.");
            builder.UserID = user;
            builder.Password = endpoint.Password ?? "";
        }

        return builder.ConnectionString;
    }

    public async Task<ConnectionProbeResult> TestAsync(SqlEndpoint endpoint, CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = BuildConnectionString(endpoint);
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            await command.ExecuteScalarAsync(cancellationToken);
            return new ConnectionProbeResult { Succeeded = true, Message = "Bağlantı başarılı." };
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Bağlantı denemesi başarısız. {Detail}", SensitiveDataSanitizer.Clean(exception.ToString()));
            return new ConnectionProbeResult { Succeeded = false, Message = "SQL Server bağlantısı kurulamadı." };
        }
    }
}
