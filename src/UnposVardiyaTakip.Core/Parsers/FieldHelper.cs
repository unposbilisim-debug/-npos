using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace UnposVardiyaTakip.Core;

public static class FieldKeys
{
    public static readonly HashSet<string> Volume = ["miktar", "miktari", "volume", "litre", "liter", "liters", "qty", "quantity", "satismiktari", "fuelvolume", "lt"];
    public static readonly HashSet<string> Amount = ["tutar", "tutari", "amount", "total", "satistutari", "priceamount", "bedel"];
    public static readonly HashSet<string> Price = ["birimfiyat", "unitprice", "price", "fiyat", "unit", "birim"];
    public static readonly HashSet<string> Pump = ["pompa", "pump", "pumpno", "pompano", "pompakodu"];
    public static readonly HashSet<string> Nozzle = ["tabanca", "nozzle", "hose", "tabancano", "tabancakodu"];
    public static readonly HashSet<string> Fuel = ["yakit", "yakitadi", "fuel", "product", "stok", "stokkodu", "grade", "urun"];
    public static readonly HashSet<string> Attendant = ["pompaci", "attendant", "personel", "personelkodu", "perskodu", "cashier", "operator", "pn"];
    public static readonly HashSet<string> Payment = ["odeme", "odemeturu", "odemekodu", "payment", "paytype", "satistipi", "tip"];
    public static readonly HashSet<string> Time = ["tarih", "datetime", "time", "saat", "satistarihi", "zaman", "date"];
    public static readonly HashSet<string> Plate = ["plaka", "plate", "vehicle", "arac"];
    public static readonly HashSet<string> Customer = ["cari", "customer", "musteri", "kart", "cardno", "filo"];
    public static readonly HashSet<string> Seq = ["sira", "seq", "no", "id", "satissirano", "fisno"];
    public static readonly HashSet<string> ShiftNo = ["vardiyano", "vardiyanumarasi", "shiftno", "vardiya", "numarasi", "no"];
    public static readonly HashSet<string> Station = ["istasyon", "station", "istasyonadi", "bayi"];
    public static readonly HashSet<string> Open = ["acilis", "opening", "start", "startindex", "acilisendeks"];
    public static readonly HashSet<string> Close = ["kapanis", "closing", "end", "endindex", "kapanisendeks"];
}

public static class FieldHelper
{
    public static string Fold(string? value)
    {
        var text = (value ?? "").Trim().ToLowerInvariant()
            .Replace("ı", "i").Replace("İ", "i").Replace("ş", "s").Replace("ğ", "g")
            .Replace("ü", "u").Replace("ü", "u").Replace("ö", "o").Replace("ç", "c");
        text = text.Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        }
        return sb.ToString();
    }

    public static string Pick(IDictionary<string, string> data, HashSet<string> keys)
    {
        foreach (var pair in data)
        {
            if (keys.Contains(Fold(pair.Key)) && !string.IsNullOrWhiteSpace(pair.Value))
                return pair.Value.Trim();
        }
        return "";
    }

    public static double ToNumber(string? value, double divisor = 1)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var original = value.Trim();
        var text = original.Replace(" ", "");
        if (text.Contains(',') && text.Contains('.'))
        {
            text = text.LastIndexOf(',') > text.LastIndexOf('.')
                ? text.Replace(".", "").Replace(",", ".")
                : text.Replace(",", "");
        }
        else if (text.Contains(','))
        {
            text = text.Replace(".", "").Replace(",", ".");
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            var digits = Regex.Replace(original, @"[^0-9,.\-]", "").Replace(",", ".");
            if (!double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                return 0;
        }

        if (divisor > 1 && !original.Contains('.') && !original.Contains(','))
            number /= divisor;
        return number;
    }

    public static string TodayIso() => DateTime.Now.ToString("yyyy-MM-dd");

    public static string NormalizeDate(string? value)
    {
        var text = (value ?? "").Trim();
        if (string.IsNullOrEmpty(text)) return TodayIso();
        string[] formats =
        [
            "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "dd.MM.yyyy", "dd/MM/yyyy",
            "yyyyMMdd", "dd-MM-yyyy", "yyyy.MM.dd", "yyyy-MM-dd HH:mm:ss"
        ];
        var slice = text.Length >= 19 ? text[..19] : text;
        foreach (var fmt in formats)
        {
            if (DateTime.TryParseExact(slice, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed.ToString("yyyy-MM-dd");
        }
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var any))
            return any.ToString("yyyy-MM-dd");
        var digits = Regex.Replace(text, @"\D", "");
        if (digits.Length >= 8)
        {
            if (DateTime.TryParseExact(digits[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ymd))
                return ymd.ToString("yyyy-MM-dd");
            if (DateTime.TryParseExact(digits[..8], "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dmy))
                return dmy.ToString("yyyy-MM-dd");
        }
        return TodayIso();
    }

    public static string Money(double value) =>
        value.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"));

    public static string Liter(double value) =>
        value.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"));
}
