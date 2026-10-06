using Microsoft.Data.Sqlite;

namespace UnposVardiyaTakip.Core;

public sealed class VardiyaStore
{
    private readonly string _connectionString;

    public VardiyaStore()
    {
        AppPaths.Ensure();
        _connectionString = new SqliteConnectionStringBuilder { DataSource = AppPaths.DatabaseFile }.ToString();
        using var db = Open();
        db.Execute("""
            CREATE TABLE IF NOT EXISTS shifts (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              station_name TEXT,
              shift_date TEXT,
              shift_no TEXT,
              brand TEXT,
              status TEXT,
              source_file TEXT,
              source_hash TEXT UNIQUE,
              opened_at TEXT,
              closed_at TEXT,
              created_at TEXT
            );
            CREATE TABLE IF NOT EXISTS sales (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              shift_id INTEGER,
              seq INTEGER,
              sold_at TEXT,
              pump TEXT,
              nozzle TEXT,
              fuel TEXT,
              volume REAL,
              amount REAL,
              unit_price REAL,
              attendant TEXT,
              payment_code TEXT,
              payment_name TEXT,
              plate TEXT,
              customer TEXT
            );
            CREATE TABLE IF NOT EXISTS meters (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              shift_id INTEGER,
              pump TEXT,
              nozzle TEXT,
              fuel TEXT,
              opening REAL,
              closing REAL,
              sales_volume REAL,
              variance REAL
            );
            CREATE TABLE IF NOT EXISTS ingest_logs (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              path TEXT,
              status TEXT,
              message TEXT,
              created_at TEXT
            );
            """);
    }

    public SqliteConnection Open()
    {
        var db = new SqliteConnection(_connectionString);
        db.Open();
        return db;
    }
}

internal static class SqliteExtensions
{
    public static void Execute(this SqliteConnection db, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
        cmd.ExecuteNonQuery();
    }

    public static SqliteDataReader Query(this SqliteConnection db, string sql, params (string Name, object? Value)[] args)
    {
        var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
        return cmd.ExecuteReader();
    }

    public static object? Scalar(this SqliteConnection db, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
        return cmd.ExecuteScalar();
    }
}
