using System.Text.Json;

namespace UnposVardiyaTakip.Core;

public sealed class AppConfig
{
    public string StationName { get; set; } = "İstasyon";
    public string WatchPath { get; set; } = "";
    public string Brand { get; set; } = "auto";
    public int PollSeconds { get; set; } = 8;
    public int VolumeDivisor { get; set; } = 100;
    public int AmountDivisor { get; set; } = 100;
    public int PriceDivisor { get; set; } = 100;
    public Dictionary<string, string> PaymentMap { get; set; } = new(DefaultPaymentMap, StringComparer.OrdinalIgnoreCase);

    public static readonly Dictionary<string, string> DefaultPaymentMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["100"] = "Nakit",
        ["101"] = "Kredi Kartı",
        ["102"] = "TTS / Filo",
        ["103"] = "Veresiye",
        ["104"] = "Puan / Pont",
        ["0"] = "Nakit",
        ["1"] = "Kredi Kartı",
        ["2"] = "TTS / Filo",
        ["3"] = "Veresiye",
        ["NAKIT"] = "Nakit",
        ["CASH"] = "Nakit",
        ["KREDI"] = "Kredi Kartı",
        ["KREDIKARTI"] = "Kredi Kartı",
        ["KART"] = "Kredi Kartı",
        ["CARD"] = "Kredi Kartı",
        ["TTS"] = "TTS / Filo",
        ["FILO"] = "TTS / Filo",
        ["FLEET"] = "TTS / Filo",
        ["VERESIYE"] = "Veresiye",
        ["CARI"] = "Veresiye",
        ["CREDIT"] = "Veresiye",
        ["PUAN"] = "Puan / Pont",
        ["PONT"] = "Puan / Pont",
    };

    public string PaymentName(string? code)
    {
        var key = (code ?? "").Trim();
        if (string.IsNullOrEmpty(key)) return "Diğer";
        return PaymentMap.TryGetValue(key, out var name) ? name : key;
    }

    public static AppConfig Load()
    {
        AppPaths.Ensure();
        if (!File.Exists(AppPaths.ConfigFile))
        {
            var fresh = new AppConfig();
            fresh.Save();
            return fresh;
        }

        var json = File.ReadAllText(AppPaths.ConfigFile);
        var loaded = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        var map = new Dictionary<string, string>(DefaultPaymentMap, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in loaded.PaymentMap)
            map[pair.Key] = pair.Value;
        loaded.PaymentMap = map;
        if (loaded.PollSeconds < 3) loaded.PollSeconds = 8;
        if (loaded.VolumeDivisor < 1) loaded.VolumeDivisor = 100;
        if (loaded.AmountDivisor < 1) loaded.AmountDivisor = 100;
        if (loaded.PriceDivisor < 1) loaded.PriceDivisor = 100;
        return loaded;
    }

    public void Save()
    {
        AppPaths.Ensure();
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(AppPaths.ConfigFile, json);
    }
}

public static class AppPaths
{
    public static string Root => Path.Combine(AppContext.BaseDirectory);
    public static string DataDir => Path.Combine(Root, "data");
    public static string SamplesDir
    {
        get
        {
            string[] candidates =
            [
                Path.Combine(Root, "samples"),
                Path.GetFullPath(Path.Combine(Root, "..", "..", "..", "..", "samples")),
                Path.GetFullPath(Path.Combine(Root, "..", "..", "..", "..", "..", "samples")),
            ];
            return candidates.FirstOrDefault(Directory.Exists) ?? Path.Combine(Root, "samples");
        }
    }
    public static string ConfigFile => Path.Combine(DataDir, "config.json");
    public static string DatabaseFile => Path.Combine(DataDir, "vardiya.db");
    public static string ProcessedDir => Path.Combine(DataDir, "processed");
    public static string ExportsDir => Path.Combine(DataDir, "exports");

    public static void Ensure()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(ProcessedDir);
        Directory.CreateDirectory(ExportsDir);
    }
}
