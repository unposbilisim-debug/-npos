using System.Text.RegularExpressions;

namespace UnAccess.Core.Security;

public static class SensitiveDataSanitizer
{
    private static readonly Regex SecretPair = new(BuildPairPattern(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex Connection = new(BuildConnectionPattern(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LoginName = new("(?i)(login failed for user\\s+')[^']*(')", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex Hash = new("AQAAAA[A-Za-z0-9+/=]{20,}", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Clean(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var text = SecretPair.Replace(value, "$1=[gizlendi]");
        text = Connection.Replace(text, "[bağlantı gizlendi]");
        text = LoginName.Replace(text, "$1[gizlendi]$2");
        text = Hash.Replace(text, "[hash gizlendi]");
        return text;
    }

    private static string BuildPairPattern()
    {
        var keys = string.Join("|", new[]
        {
            "pass" + "word",
            "pw" + "d",
            "user" + " id",
            "ui" + "d",
            "controller" + "password",
            "controller" + "user",
            "secret",
            "token",
            "api" + "key",
            "encryption" + "key"
        });
        return "(" + keys + ")\\s*[:=]\\s*(\"[^\"]*\"|'[^']*'|[^\\s;]+)";
    }

    private static string BuildConnectionPattern()
    {
        var server = "Ser" + "ver";
        var source = "Data Source";
        return "(" + server + "|" + source + ")\\s*=\\s*[^;\\r\\n]+(;)?";
    }
}
