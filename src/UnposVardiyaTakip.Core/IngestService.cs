using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace UnposVardiyaTakip.Core;

public sealed class IngestResult
{
    public ShiftRow? Shift { get; init; }
    public string Status { get; init; } = "";
    public string Message { get; init; } = "";
}

public sealed class IngestService
{
    public static readonly HashSet<string> WatchSuffixes = [".xml", ".txt", ".csv", ".zip", ".gz"];
    private readonly VardiyaStore _store = new();

    public static bool IsWatchFile(string path)
    {
        if (!File.Exists(path)) return false;
        var name = Path.GetFileName(path);
        if (name.StartsWith('.')) return false;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return WatchSuffixes.Contains(ext) || name.EndsWith(".xml.gz", StringComparison.OrdinalIgnoreCase);
    }

    public static string FileHash(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public IngestResult Ingest(string path, bool copyProcessed = true)
    {
        var config = AppConfig.Load();
        using var db = _store.Open();
        using var tx = db.BeginTransaction();
        try
        {
            var digest = FileHash(path);
            var isLive = Path.GetFileName(path).StartsWith("sales", StringComparison.OrdinalIgnoreCase);
            var existingId = db.Scalar("SELECT id FROM shifts WHERE source_hash=$h", ("$h", digest));
            if (existingId is not null and not DBNull && !isLive)
            {
                Log(db, path, "skip", "Aynı dosya daha önce işlendi.");
                tx.Commit();
                return new IngestResult { Shift = LoadShift(db, Convert.ToInt64(existingId)), Status = "skip", Message = "Aynı dosya daha önce işlendi." };
            }

            var divisors = new Dictionary<string, double>
            {
                ["volume"] = config.VolumeDivisor,
                ["amount"] = config.AmountDivisor,
                ["price"] = config.PriceDivisor,
            };
            var parsed = AutomationParser.Parse(path, config.Brand, divisors);
            if (string.IsNullOrWhiteSpace(parsed.StationName))
                parsed.StationName = config.StationName;

            long? shiftId = existingId is long or int or short ? Convert.ToInt64(existingId) : null;
            if (isLive)
            {
                var closed = db.Scalar(
                    "SELECT id FROM shifts WHERE status='closed' AND shift_date=$d AND shift_no=$n AND station_name=$s",
                    ("$d", parsed.ShiftDate), ("$n", parsed.ShiftNo), ("$s", parsed.StationName));
                if (closed is not null and not DBNull)
                {
                    Log(db, path, "skip", "Bu vardiya kapanmış arşivden alındı; canlı dosya yok sayıldı.");
                    tx.Commit();
                    return new IngestResult { Shift = LoadShift(db, Convert.ToInt64(closed)), Status = "skip", Message = "Canlı dosya yok sayıldı." };
                }
                var open = db.Scalar(
                    "SELECT id FROM shifts WHERE status='open' AND shift_date=$d AND station_name=$s",
                    ("$d", parsed.ShiftDate), ("$s", parsed.StationName));
                if (open is not null and not DBNull)
                    shiftId = Convert.ToInt64(open);
            }
            else
            {
                var openMatch = db.Scalar(
                    "SELECT id FROM shifts WHERE status='open' AND shift_date=$d AND shift_no=$n AND station_name=$s",
                    ("$d", parsed.ShiftDate), ("$n", parsed.ShiftNo), ("$s", parsed.StationName));
                if (openMatch is not null and not DBNull)
                {
                    shiftId = Convert.ToInt64(openMatch);
                    parsed.Status = "closed";
                }
            }

            if (shiftId is null)
            {
                db.Execute("""
                    INSERT INTO shifts(station_name,shift_date,shift_no,brand,status,source_file,source_hash,opened_at,closed_at,created_at)
                    VALUES($st,$d,$n,$b,$u,$f,$h,$o,$c,$t)
                    """,
                    ("$st", parsed.StationName), ("$d", parsed.ShiftDate), ("$n", parsed.ShiftNo),
                    ("$b", parsed.Brand), ("$u", parsed.Status), ("$f", path), ("$h", digest),
                    ("$o", parsed.OpenedAt), ("$c", parsed.ClosedAt), ("$t", DateTime.Now.ToString("s")));
                shiftId = Convert.ToInt64(db.Scalar("SELECT last_insert_rowid()")!);
            }
            else
            {
                db.Execute("""
                    UPDATE shifts SET source_file=$f, source_hash=$h, shift_no=$n, brand=$b, status=$u, opened_at=$o, closed_at=$c
                    WHERE id=$id
                    """,
                    ("$f", path), ("$h", digest), ("$n", parsed.ShiftNo), ("$b", parsed.Brand),
                    ("$u", parsed.Status), ("$o", parsed.OpenedAt), ("$c", parsed.ClosedAt), ("$id", shiftId));
            }

            ApplyRows(db, shiftId.Value, parsed, config);

            if (copyProcessed)
            {
                try
                {
                    AppPaths.Ensure();
                    var target = Path.Combine(AppPaths.ProcessedDir, $"{parsed.ShiftDate}_v{parsed.ShiftNo}_{Path.GetFileName(path)}");
                    if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                        File.Copy(path, target, true);
                }
                catch { /* paylaşım kilitli olabilir */ }
            }

            Log(db, path, "ok", $"{parsed.Sales.Count} satış aktarıldı.");
            tx.Commit();
            return new IngestResult { Shift = LoadShift(db, shiftId.Value), Status = "ok", Message = $"{parsed.Sales.Count} satış aktarıldı." };
        }
        catch (Exception ex)
        {
            try { tx.Rollback(); } catch { /* ignore */ }
            using var db2 = _store.Open();
            Log(db2, path, "error", ex.Message);
            return new IngestResult { Status = "error", Message = ex.Message };
        }
    }

    private static void ApplyRows(SqliteConnection db, long shiftId, ParsedShift parsed, AppConfig config)
    {
        db.Execute("DELETE FROM sales WHERE shift_id=$id", ("$id", shiftId));
        db.Execute("DELETE FROM meters WHERE shift_id=$id", ("$id", shiftId));
        var byNozzle = new Dictionary<(string, string, string), double>();
        foreach (var sale in parsed.Sales)
        {
            db.Execute("""
                INSERT INTO sales(shift_id,seq,sold_at,pump,nozzle,fuel,volume,amount,unit_price,attendant,payment_code,payment_name,plate,customer)
                VALUES($id,$seq,$at,$p,$n,$f,$v,$a,$u,$att,$pc,$pn,$pl,$c)
                """,
                ("$id", shiftId), ("$seq", sale.Seq), ("$at", sale.SoldAt), ("$p", sale.Pump), ("$n", sale.Nozzle),
                ("$f", sale.Fuel), ("$v", sale.Volume), ("$a", sale.Amount), ("$u", sale.UnitPrice),
                ("$att", sale.Attendant), ("$pc", sale.PaymentCode), ("$pn", config.PaymentName(sale.PaymentCode)),
                ("$pl", sale.Plate), ("$c", sale.Customer));
            var key = (sale.Pump, sale.Nozzle, sale.Fuel);
            byNozzle[key] = byNozzle.GetValueOrDefault(key) + sale.Volume;
        }

        if (parsed.Meters.Count > 0)
        {
            foreach (var meter in parsed.Meters)
            {
                if (!byNozzle.TryGetValue((meter.Pump, meter.Nozzle, meter.Fuel), out var sold))
                    byNozzle.TryGetValue((meter.Pump, meter.Nozzle, ""), out sold);
                var delta = meter.Closing - meter.Opening;
                db.Execute("""
                    INSERT INTO meters(shift_id,pump,nozzle,fuel,opening,closing,sales_volume,variance)
                    VALUES($id,$p,$n,$f,$o,$c,$s,$v)
                    """,
                    ("$id", shiftId), ("$p", meter.Pump), ("$n", meter.Nozzle), ("$f", meter.Fuel),
                    ("$o", meter.Opening), ("$c", meter.Closing), ("$s", sold), ("$v", Math.Round(delta - sold, 3)));
            }
        }
        else
        {
            foreach (var pair in byNozzle.OrderBy(p => p.Key.Item1).ThenBy(p => p.Key.Item2))
            {
                db.Execute("""
                    INSERT INTO meters(shift_id,pump,nozzle,fuel,opening,closing,sales_volume,variance)
                    VALUES($id,$p,$n,$f,0,$c,$s,0)
                    """,
                    ("$id", shiftId), ("$p", pair.Key.Item1), ("$n", pair.Key.Item2), ("$f", pair.Key.Item3),
                    ("$c", pair.Value), ("$s", pair.Value));
            }
        }
    }

    private static void Log(SqliteConnection db, string path, string status, string message)
    {
        db.Execute("INSERT INTO ingest_logs(path,status,message,created_at) VALUES($p,$s,$m,$t)",
            ("$p", path), ("$s", status), ("$m", message), ("$t", DateTime.Now.ToString("s")));
    }

    public List<ShiftRow> ListShifts()
    {
        using var db = _store.Open();
        var basics = new List<(long Id, string Station, string Date, string No, string Brand, string Status, string File, string Hash, string? Opened, string? Closed)>();
        using (var reader = db.Query("SELECT * FROM shifts ORDER BY shift_date DESC, id DESC"))
        {
            while (reader.Read())
            {
                basics.Add((
                    Convert.ToInt64(reader["id"]),
                    reader["station_name"]?.ToString() ?? "",
                    reader["shift_date"]?.ToString() ?? "",
                    reader["shift_no"]?.ToString() ?? "",
                    reader["brand"]?.ToString() ?? "",
                    reader["status"]?.ToString() ?? "",
                    reader["source_file"]?.ToString() ?? "",
                    reader["source_hash"]?.ToString() ?? "",
                    reader["opened_at"] as string,
                    reader["closed_at"] as string
                ));
            }
        }
        return basics.Select(item => Enrich(db, item)).ToList();
    }

    public ShiftSummary? GetSummary(long id)
    {
        using var db = _store.Open();
        var shift = LoadShift(db, id);
        if (shift is null) return null;
        var sales = new List<SaleRow>();
        using (var reader = db.Query("SELECT * FROM sales WHERE shift_id=$id ORDER BY seq, id", ("$id", id)))
        {
            while (reader.Read())
            {
                sales.Add(new SaleRow
                {
                    Seq = reader.GetInt32(reader.GetOrdinal("seq")),
                    SoldAt = reader["sold_at"]?.ToString() ?? "",
                    Pump = reader["pump"]?.ToString() ?? "",
                    Nozzle = reader["nozzle"]?.ToString() ?? "",
                    Fuel = reader["fuel"]?.ToString() ?? "",
                    Volume = Convert.ToDouble(reader["volume"]),
                    Amount = Convert.ToDouble(reader["amount"]),
                    UnitPrice = Convert.ToDouble(reader["unit_price"]),
                    Attendant = reader["attendant"]?.ToString() ?? "",
                    PaymentCode = reader["payment_code"]?.ToString() ?? "",
                    PaymentName = reader["payment_name"]?.ToString() ?? "",
                    Plate = reader["plate"]?.ToString() ?? "",
                    Customer = reader["customer"]?.ToString() ?? "",
                });
            }
        }
        var meters = new List<MeterRow>();
        using (var reader = db.Query("SELECT * FROM meters WHERE shift_id=$id", ("$id", id)))
        {
            while (reader.Read())
            {
                meters.Add(new MeterRow
                {
                    Pump = reader["pump"]?.ToString() ?? "",
                    Nozzle = reader["nozzle"]?.ToString() ?? "",
                    Fuel = reader["fuel"]?.ToString() ?? "",
                    Opening = Convert.ToDouble(reader["opening"]),
                    Closing = Convert.ToDouble(reader["closing"]),
                    SalesVolume = Convert.ToDouble(reader["sales_volume"]),
                    Variance = Convert.ToDouble(reader["variance"]),
                });
            }
        }

        var byFuel = sales.GroupBy(s => string.IsNullOrWhiteSpace(s.Fuel) ? "Yakıt" : s.Fuel)
            .Select(g => new GroupTotal { Name = g.Key, Volume = g.Sum(x => x.Volume), Amount = g.Sum(x => x.Amount), Count = g.Count() }).ToList();
        var byAtt = sales.GroupBy(s => string.IsNullOrWhiteSpace(s.Attendant) ? "Pompacı" : s.Attendant)
            .Select(g => new GroupTotal { Name = g.Key, Volume = g.Sum(x => x.Volume), Amount = g.Sum(x => x.Amount), Count = g.Count() }).ToList();
        var byPay = sales.GroupBy(s => string.IsNullOrWhiteSpace(s.PaymentName) ? s.PaymentCode : s.PaymentName)
            .Select(g => new GroupTotal { Name = g.Key, Volume = g.Sum(x => x.Volume), Amount = g.Sum(x => x.Amount), Count = g.Count() }).ToList();

        return new ShiftSummary
        {
            Shift = shift,
            Count = sales.Count,
            TotalVolume = sales.Sum(s => s.Volume),
            TotalAmount = sales.Sum(s => s.Amount),
            CashAmount = byPay.FirstOrDefault(x => x.Name == "Nakit")?.Amount ?? 0,
            ByFuel = byFuel,
            ByAttendant = byAtt,
            ByPayment = byPay,
            Meters = meters,
            Sales = sales,
        };
    }

    public List<IngestLogRow> RecentLogs(int limit = 12)
    {
        using var db = _store.Open();
        using var reader = db.Query("SELECT path,status,message,created_at FROM ingest_logs ORDER BY id DESC LIMIT $n", ("$n", limit));
        var list = new List<IngestLogRow>();
        while (reader.Read())
        {
            list.Add(new IngestLogRow
            {
                Path = reader["path"]?.ToString() ?? "",
                Status = reader["status"]?.ToString() ?? "",
                Message = reader["message"]?.ToString() ?? "",
                CreatedAt = reader["created_at"]?.ToString() ?? "",
            });
        }
        return list;
    }

    public (int Shifts, int Open, double Volume, double Amount) Totals()
    {
        var shifts = ListShifts();
        return (shifts.Count, shifts.Count(s => s.Status == "open"), shifts.Sum(s => s.TotalVolume), shifts.Sum(s => s.TotalAmount));
    }

    private static ShiftRow Enrich(SqliteConnection db,
        (long Id, string Station, string Date, string No, string Brand, string Status, string File, string Hash, string? Opened, string? Closed) item)
    {
        double volume = 0, amount = 0, cash = 0;
        var count = 0;
        using (var agg = db.Query("SELECT COUNT(*) c, COALESCE(SUM(volume),0) v, COALESCE(SUM(amount),0) a FROM sales WHERE shift_id=$id", ("$id", item.Id)))
        {
            if (agg.Read())
            {
                count = Convert.ToInt32(agg["c"]);
                volume = Convert.ToDouble(agg["v"]);
                amount = Convert.ToDouble(agg["a"]);
            }
        }
        using (var cashR = db.Query("SELECT COALESCE(SUM(amount),0) a FROM sales WHERE shift_id=$id AND payment_name='Nakit'", ("$id", item.Id)))
        {
            if (cashR.Read()) cash = Convert.ToDouble(cashR["a"]);
        }
        return new ShiftRow
        {
            Id = item.Id,
            StationName = item.Station,
            ShiftDate = item.Date,
            ShiftNo = item.No,
            Brand = item.Brand,
            Status = item.Status,
            SourceFile = item.File,
            SourceHash = item.Hash,
            OpenedAt = item.Opened,
            ClosedAt = item.Closed,
            Count = count,
            TotalVolume = volume,
            TotalAmount = amount,
            CashAmount = cash,
        };
    }

    private static ShiftRow? LoadShift(SqliteConnection db, long id)
    {
        using var reader = db.Query("SELECT * FROM shifts WHERE id=$id", ("$id", id));
        if (!reader.Read()) return null;
        var item = (
            Convert.ToInt64(reader["id"]),
            reader["station_name"]?.ToString() ?? "",
            reader["shift_date"]?.ToString() ?? "",
            reader["shift_no"]?.ToString() ?? "",
            reader["brand"]?.ToString() ?? "",
            reader["status"]?.ToString() ?? "",
            reader["source_file"]?.ToString() ?? "",
            reader["source_hash"]?.ToString() ?? "",
            reader["opened_at"] as string,
            reader["closed_at"] as string
        );
        reader.Dispose();
        return Enrich(db, item);
    }
}
