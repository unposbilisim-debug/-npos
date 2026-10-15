namespace UnAccess.Data.Infrastructure;

internal static class LikePattern
{
    public static string Contains(string value)
    {
        var escaped = value.Trim()
            .Replace("[", "[[]", StringComparison.Ordinal)
            .Replace("%", "[%]", StringComparison.Ordinal)
            .Replace("_", "[_]", StringComparison.Ordinal);
        return $"%{escaped}%";
    }
}
