using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using UnAccess.Core.Exceptions;

namespace UnAccess.Data.Infrastructure;

internal static class SqlIdentifiers
{
    private static readonly Regex Name = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string RequireDatabase(string? name)
    {
        var text = (name ?? "").Trim();
        if (text.Length == 0 || text.Length > 128 || !Name.IsMatch(text))
            throw new UnAccessException("Veritabanı adı geçersiz.");
        return text;
    }

    public static string DatabaseName(string connectionString) =>
        RequireDatabase(new SqlConnectionStringBuilder(connectionString).InitialCatalog);

    public static string WithCatalog(string connectionString, string catalog)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = RequireDatabase(catalog)
        };
        return builder.ConnectionString;
    }

    public static string DataSource(string connectionString) =>
        new SqlConnectionStringBuilder(connectionString).DataSource;
}
