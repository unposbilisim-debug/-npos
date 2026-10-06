using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace UnposVardiyaTakip.Core;

public static class TurpakParser
{
    private static readonly string[] Encodings = ["utf-8", "utf-8-sig", "windows-1254", "iso-8859-9", "latin1"];

    public static ParsedShift Parse(string path, IReadOnlyDictionary<string, double>? divisors = null)
    {
        var div = new Dictionary<string, double>
        {
            ["volume"] = divisors != null && divisors.TryGetValue("volume", out var v) ? v : 100,
            ["amount"] = divisors != null && divisors.TryGetValue("amount", out var a) ? a : 100,
            ["price"] = divisors != null && divisors.TryGetValue("price", out var p) ? p : 100,
        };

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var raw = ReadBytes(path);
        var root = ParseXml(raw);
        var (saleMaps, meterMaps, header) = Walk(root);

        var unique = new List<Dictionary<string, string>>();
        var seen = new HashSet<string>();
        foreach (var item in saleMaps)
        {
            var fingerprint = string.Join("|",
                FieldHelper.Pick(item, FieldKeys.Seq),
                FieldHelper.Pick(item, FieldKeys.Time),
                FieldHelper.Pick(item, FieldKeys.Pump),
                FieldHelper.Pick(item, FieldKeys.Nozzle),
                FieldHelper.Pick(item, FieldKeys.Volume),
                FieldHelper.Pick(item, FieldKeys.Amount));
            if (!seen.Add(fingerprint)) continue;
            unique.Add(item);
        }

        var fileName = Path.GetFileName(path);
        var shift = new ParsedShift
        {
            Brand = "turpak",
            SourceName = fileName,
            StationName = FieldHelper.Pick(header, FieldKeys.Station),
            ShiftDate = FieldHelper.NormalizeDate(First(FieldHelper.Pick(header, FieldKeys.Time), Path.GetFileNameWithoutExtension(path))),
            ShiftNo = First(FieldHelper.Pick(header, FieldKeys.ShiftNo), "1"),
            Status = fileName.StartsWith("sales", StringComparison.OrdinalIgnoreCase) ? "open" : "closed",
        };
        if (unique.Count == 0)
            throw new InvalidOperationException("XML içinde satış kaydı bulunamadı.");

        for (var i = 0; i < unique.Count; i++)
            shift.Sales.Add(SaleFrom(unique[i], i + 1, div));
        if (shift.Sales.Count > 0 && string.IsNullOrWhiteSpace(FieldHelper.Pick(header, FieldKeys.Time)))
            shift.ShiftDate = FieldHelper.NormalizeDate(shift.Sales[0].SoldAt);
        shift.OpenedAt = string.IsNullOrWhiteSpace(shift.Sales[0].SoldAt) ? null : shift.Sales[0].SoldAt;
        shift.ClosedAt = string.IsNullOrWhiteSpace(shift.Sales[^1].SoldAt) ? null : shift.Sales[^1].SoldAt;
        foreach (var meter in meterMaps)
            shift.Meters.Add(MeterFrom(meter, div));
        if (string.IsNullOrWhiteSpace(shift.ShiftDate))
            shift.ShiftDate = FieldHelper.TodayIso();
        return shift;
    }

    private static string First(string a, string b) => string.IsNullOrWhiteSpace(a) ? b : a;

    private static byte[] ReadBytes(string path)
    {
        var data = File.ReadAllBytes(path);
        var name = Path.GetFileName(path).ToLowerInvariant();
        if (name.EndsWith(".gz") || (data.Length >= 2 && data[0] == 0x1f && data[1] == 0x8b))
            data = InflateGzip(data);
        if (name.EndsWith(".zip") || (data.Length >= 2 && data[0] == (byte)'P' && data[1] == (byte)'K'))
        {
            using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
            var entry = zip.Entries.FirstOrDefault(e =>
                e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                e.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) ?? zip.Entries[0];
            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            data = ms.ToArray();
        }
        return data;
    }

    private static byte[] InflateGzip(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private static XElement ParseXml(byte[] raw)
    {
        Exception? last = null;
        foreach (var name in Encodings)
        {
            try
            {
                var encoding = name switch
                {
                    "utf-8-sig" => new UTF8Encoding(true),
                    "windows-1254" => Encoding.GetEncoding(1254),
                    "iso-8859-9" => Encoding.GetEncoding("iso-8859-9"),
                    "latin1" => Encoding.Latin1,
                    _ => Encoding.UTF8,
                };
                var text = encoding.GetString(raw);
                return XElement.Parse(text);
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }
        try { return XElement.Parse(Encoding.UTF8.GetString(raw)); }
        catch (Exception ex) { throw new InvalidOperationException($"XML okunamadı: {last?.Message ?? ex.Message}", ex); }
    }

    private static string Local(XName name)
    {
        var local = name.LocalName;
        var idx = local.LastIndexOf('}');
        return idx >= 0 ? local[(idx + 1)..] : local;
    }

    private static Dictionary<string, string> ElementMap(XElement element)
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var attr in element.Attributes())
            data[Local(attr.Name)] = attr.Value;
        if (!string.IsNullOrWhiteSpace(element.Value) && !element.HasElements)
            data[Local(element.Name)] = element.Value.Trim();
        foreach (var child in element.Elements())
        {
            var name = Local(child.Name);
            if (child.HasElements || child.HasAttributes)
            {
                foreach (var nested in ElementMap(child))
                    data.TryAdd(nested.Key, nested.Value);
            }
            if (!string.IsNullOrWhiteSpace(child.Value) && !child.HasElements)
                data[name] = child.Value.Trim();
            foreach (var attr in child.Attributes())
            {
                data.TryAdd(Local(attr.Name), attr.Value);
                data.TryAdd($"{name}_{Local(attr.Name)}", attr.Value);
            }
        }
        return data;
    }

    private static bool LooksLikeSale(Dictionary<string, string> data)
    {
        var hasVolume = data.Keys.Any(k => FieldKeys.Volume.Contains(FieldHelper.Fold(k)));
        var hasAmount = data.Keys.Any(k => FieldKeys.Amount.Contains(FieldHelper.Fold(k)));
        var hasPump = data.Keys.Any(k => FieldKeys.Pump.Contains(FieldHelper.Fold(k)) || FieldKeys.Nozzle.Contains(FieldHelper.Fold(k)));
        return hasVolume && (hasAmount || hasPump);
    }

    private static bool LooksLikeMeter(Dictionary<string, string> data)
    {
        var hasOpen = data.Keys.Any(k => FieldKeys.Open.Contains(FieldHelper.Fold(k)));
        var hasClose = data.Keys.Any(k => FieldKeys.Close.Contains(FieldHelper.Fold(k)));
        return hasOpen && hasClose;
    }

    private static (List<Dictionary<string, string>> Sales, List<Dictionary<string, string>> Meters, Dictionary<string, string> Header) Walk(XElement element)
    {
        var sales = new List<Dictionary<string, string>>();
        var meters = new List<Dictionary<string, string>>();
        var header = ElementMap(element);
        var tag = FieldHelper.Fold(Local(element.Name));
        var data = ElementMap(element);
        var saleTags = new HashSet<string> { "satis", "sale", "transaction", "hareket", "fis", "kayit" };
        var meterTags = new HashSet<string> { "endeks", "meter", "sayac", "tabancaendeks", "totalizer" };

        if (saleTags.Contains(tag) || LooksLikeSale(data))
        {
            var children = element.Elements().ToList();
            if (LooksLikeSale(data) && children.Count == 0)
                sales.Add(data);
            else if (LooksLikeSale(data) && !children.Any(c => LooksLikeSale(ElementMap(c))))
                sales.Add(data);
        }

        if ((meterTags.Contains(tag) || LooksLikeMeter(data)) && LooksLikeMeter(data))
            meters.Add(data);

        foreach (var child in element.Elements())
        {
            var (childSales, childMeters, childHeader) = Walk(child);
            sales.AddRange(childSales);
            meters.AddRange(childMeters);
            foreach (var pair in childHeader)
                header.TryAdd(pair.Key, pair.Value);
        }
        return (sales, meters, header);
    }

    private static ParsedSale SaleFrom(Dictionary<string, string> data, int index, Dictionary<string, double> divisors)
    {
        var volume = FieldHelper.ToNumber(FieldHelper.Pick(data, FieldKeys.Volume), divisors["volume"]);
        var amount = FieldHelper.ToNumber(FieldHelper.Pick(data, FieldKeys.Amount), divisors["amount"]);
        var unitPrice = FieldHelper.ToNumber(FieldHelper.Pick(data, FieldKeys.Price), divisors["price"]);
        if (unitPrice == 0 && volume != 0)
            unitPrice = Math.Round(amount / volume, 4);
        var seqRaw = FieldHelper.Pick(data, FieldKeys.Seq);
        if (!int.TryParse(seqRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var seq))
            seq = index;
        return new ParsedSale
        {
            Seq = seq,
            SoldAt = FieldHelper.Pick(data, FieldKeys.Time),
            Pump = FieldHelper.Pick(data, FieldKeys.Pump),
            Nozzle = FieldHelper.Pick(data, FieldKeys.Nozzle),
            Fuel = First(FieldHelper.Pick(data, FieldKeys.Fuel), "Yakıt"),
            Volume = volume,
            Amount = amount,
            UnitPrice = unitPrice,
            Attendant = First(FieldHelper.Pick(data, FieldKeys.Attendant), "Pompacı"),
            PaymentCode = First(FieldHelper.Pick(data, FieldKeys.Payment), "NAKIT"),
            Plate = FieldHelper.Pick(data, FieldKeys.Plate),
            Customer = FieldHelper.Pick(data, FieldKeys.Customer),
        };
    }

    private static ParsedMeter MeterFrom(Dictionary<string, string> data, Dictionary<string, double> divisors)
    {
        var nozzle = FieldHelper.Pick(data, FieldKeys.Nozzle);
        if (string.IsNullOrWhiteSpace(nozzle))
        {
            foreach (var pair in data)
            {
                var folded = FieldHelper.Fold(pair.Key);
                if (folded is "no" or "numara" or "tabancano" && !string.IsNullOrWhiteSpace(pair.Value))
                {
                    nozzle = pair.Value.Trim();
                    break;
                }
            }
        }
        return new ParsedMeter
        {
            Pump = FieldHelper.Pick(data, FieldKeys.Pump),
            Nozzle = nozzle,
            Fuel = FieldHelper.Pick(data, FieldKeys.Fuel),
            Opening = FieldHelper.ToNumber(FieldHelper.Pick(data, FieldKeys.Open), divisors["volume"]),
            Closing = FieldHelper.ToNumber(FieldHelper.Pick(data, FieldKeys.Close), divisors["volume"]),
        };
    }
}
