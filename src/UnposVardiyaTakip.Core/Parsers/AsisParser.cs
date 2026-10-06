using System.Globalization;
using System.Text;

namespace UnposVardiyaTakip.Core;

public static class AsisParser
{
    private static readonly Dictionary<string, string> HeaderAliases = BuildAliases();

    private static Dictionary<string, string> BuildAliases()
    {
        var map = new Dictionary<string, string>();
        void add(HashSet<string> keys, string name)
        {
            foreach (var key in keys) map[key] = name;
        }
        add(FieldKeys.Volume, "volume");
        add(FieldKeys.Amount, "amount");
        add(FieldKeys.Price, "price");
        add(FieldKeys.Pump, "pump");
        add(FieldKeys.Nozzle, "nozzle");
        add(FieldKeys.Fuel, "fuel");
        add(FieldKeys.Attendant, "attendant");
        add(FieldKeys.Payment, "payment");
        add(FieldKeys.Time, "time");
        add(FieldKeys.Plate, "plate");
        add(FieldKeys.Customer, "customer");
        add(FieldKeys.Seq, "seq");
        add(FieldKeys.ShiftNo, "shift_no");
        add(FieldKeys.Station, "station");
        return map;
    }

    public static ParsedShift Parse(string path, IReadOnlyDictionary<string, double>? divisors = null)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var text = Decode(File.ReadAllBytes(path));
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#'))
            .ToList();
        if (lines.Count == 0)
            throw new InvalidOperationException("Asis dosyası boş.");

        var delimiter = DetectDelimiter(string.Join('\n', lines.Take(10)));
        var rows = lines.Select(l => Split(l, delimiter)).ToList();
        var headerRow = rows[0].Select(FieldHelper.Fold).ToList();
        var hasHeader = headerRow.Any(HeaderAliases.ContainsKey);
        var mapping = new Dictionary<int, string>();
        var dataRows = rows;
        if (hasHeader)
        {
            for (var i = 0; i < headerRow.Count; i++)
            {
                if (HeaderAliases.TryGetValue(headerRow[i], out var name))
                    mapping[i] = name;
            }
            dataRows = rows.Skip(1).ToList();
        }
        else
        {
            string[] defaults = ["time", "shift_no", "pump", "nozzle", "fuel", "volume", "amount", "price", "attendant", "payment"];
            for (var i = 0; i < defaults.Length; i++)
                mapping[i] = defaults[i];
        }

        var sales = new List<ParsedSale>();
        var station = "";
        var shiftNo = "1";
        var shiftDate = "";
        var index = 0;
        foreach (var row in dataRows)
        {
            index++;
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            var data = new Dictionary<string, string>();
            var max = mapping.Count == 0 ? 0 : mapping.Keys.Max();
            for (var i = 0; i <= max && i < row.Length; i++)
            {
                if (mapping.TryGetValue(i, out var name))
                    data[name] = row[i].Trim();
            }

            var volumeRaw = data.GetValueOrDefault("volume", "");
            var amountRaw = data.GetValueOrDefault("amount", "");
            var priceRaw = data.GetValueOrDefault("price", "");
            var volumeDiv = volumeRaw.Contains('.') || volumeRaw.Contains(',') ? 1 : (divisors != null && divisors.TryGetValue("volume", out var vd) ? vd : 1);
            var amountDiv = amountRaw.Contains('.') || amountRaw.Contains(',') ? 1 : (divisors != null && divisors.TryGetValue("amount", out var ad) ? ad : 1);
            var priceDiv = priceRaw.Contains('.') || priceRaw.Contains(',') ? 1 : (divisors != null && divisors.TryGetValue("price", out var pd) ? pd : 1);
            var volume = FieldHelper.ToNumber(volumeRaw, volumeDiv);
            var amount = FieldHelper.ToNumber(amountRaw, amountDiv);
            var unitPrice = FieldHelper.ToNumber(priceRaw, priceDiv);
            if (unitPrice == 0 && volume != 0)
                unitPrice = Math.Round(amount / volume, 4);
            if (volume == 0 && amount == 0) continue;

            if (string.IsNullOrWhiteSpace(station))
                station = data.GetValueOrDefault("station", "");
            shiftNo = data.GetValueOrDefault("shift_no") is { Length: > 0 } sn ? sn : shiftNo;
            if (string.IsNullOrWhiteSpace(shiftDate))
                shiftDate = FieldHelper.NormalizeDate(data.GetValueOrDefault("time"));
            if (!int.TryParse(data.GetValueOrDefault("seq"), NumberStyles.Any, CultureInfo.InvariantCulture, out var seqValue))
                seqValue = index;

            sales.Add(new ParsedSale
            {
                Seq = seqValue,
                SoldAt = data.GetValueOrDefault("time", ""),
                Pump = data.GetValueOrDefault("pump", ""),
                Nozzle = data.GetValueOrDefault("nozzle", ""),
                Fuel = string.IsNullOrWhiteSpace(data.GetValueOrDefault("fuel")) ? "Yakıt" : data["fuel"],
                Volume = volume,
                Amount = amount,
                UnitPrice = unitPrice,
                Attendant = string.IsNullOrWhiteSpace(data.GetValueOrDefault("attendant")) ? "Pompacı" : data["attendant"],
                PaymentCode = string.IsNullOrWhiteSpace(data.GetValueOrDefault("payment")) ? "NAKIT" : data["payment"],
                Plate = data.GetValueOrDefault("plate", ""),
                Customer = data.GetValueOrDefault("customer", ""),
            });
        }

        if (sales.Count == 0)
            throw new InvalidOperationException("Asis dosyasında satış satırı bulunamadı.");

        var shift = new ParsedShift
        {
            Brand = "asis",
            SourceName = Path.GetFileName(path),
            StationName = station,
            ShiftDate = string.IsNullOrWhiteSpace(shiftDate) ? FieldHelper.NormalizeDate(Path.GetFileNameWithoutExtension(path)) : shiftDate,
            ShiftNo = string.IsNullOrWhiteSpace(shiftNo) ? "1" : shiftNo,
            Status = "closed",
            OpenedAt = string.IsNullOrWhiteSpace(sales[0].SoldAt) ? null : sales[0].SoldAt,
            ClosedAt = string.IsNullOrWhiteSpace(sales[^1].SoldAt) ? null : sales[^1].SoldAt,
        };
        shift.Sales.AddRange(sales);
        return shift;
    }

    private static string Decode(byte[] raw)
    {
        foreach (var name in new[] { "utf-8", "windows-1254", "iso-8859-9", "latin1" })
        {
            try
            {
                var enc = name switch
                {
                    "windows-1254" => Encoding.GetEncoding(1254),
                    "iso-8859-9" => Encoding.GetEncoding("iso-8859-9"),
                    "latin1" => Encoding.Latin1,
                    _ => new UTF8Encoding(true, true),
                };
                return enc.GetString(raw);
            }
            catch (DecoderFallbackException) { }
        }
        return Encoding.Latin1.GetString(raw);
    }

    private static char DetectDelimiter(string sample)
    {
        var candidates = new[] { ';', ',', '\t', '|' };
        return candidates.MaxBy(c => sample.Count(ch => ch == c));
    }

    private static string[] Split(string line, char delimiter)
    {
        var list = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        foreach (var ch in line)
        {
            if (ch == '"') { quoted = !quoted; continue; }
            if (ch == delimiter && !quoted)
            {
                list.Add(current.ToString());
                current.Clear();
            }
            else current.Append(ch);
        }
        list.Add(current.ToString());
        return list.ToArray();
    }
}
