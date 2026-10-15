namespace UnAccess.Data.Infrastructure;

internal static class TimeZoneLookup
{
    public static bool TryFind(string? id, out TimeZoneInfo zone)
    {
        zone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        var trimmed = id.Trim();
        if (TrySystem(trimmed, out zone))
            return true;

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(trimmed, out var windowsId)
            && TrySystem(windowsId, out zone))
            return true;

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(trimmed, out var ianaId)
            && TrySystem(ianaId, out zone))
            return true;

        if (KnownWindowsIds.TryGetValue(trimmed, out var known) && TrySystem(known, out zone))
            return true;

        if (string.Equals(trimmed, "Europe/Istanbul", StringComparison.OrdinalIgnoreCase))
        {
            zone = TimeZoneInfo.CreateCustomTimeZone(
                "Europe/Istanbul",
                TimeSpan.FromHours(3),
                "Türkiye Saati",
                "Türkiye Saati");
            return true;
        }

        return false;
    }

    private static readonly Dictionary<string, string> KnownWindowsIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Europe/Istanbul"] = "Turkey Standard Time"
    };

    private static bool TrySystem(string id, out TimeZoneInfo zone)
    {
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Utc;
            return false;
        }
    }
}
