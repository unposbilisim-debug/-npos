namespace UnposVardiyaTakip.Core;

public static class AutomationParser
{
    public static string DetectBrand(string path, string preferred = "auto")
    {
        if (preferred is "turpak" or "asis") return preferred;
        var name = Path.GetFileName(path).ToLowerInvariant();
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (name.Contains("asis") || ext is ".txt" or ".csv") return "asis";
        return "turpak";
    }

    public static ParsedShift Parse(string path, string brand = "auto", IReadOnlyDictionary<string, double>? divisors = null)
    {
        var chosen = DetectBrand(path, brand);
        if (chosen == "asis")
        {
            try { return AsisParser.Parse(path, divisors); }
            catch when (brand != "asis") { return TurpakParser.Parse(path, divisors); }
        }
        try { return TurpakParser.Parse(path, divisors); }
        catch when (brand != "turpak") { return AsisParser.Parse(path, divisors); }
    }
}
