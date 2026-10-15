namespace UnAccess.Data.Infrastructure;

internal static class BackupDisk
{
    public static string FreeSpace(string dataSource, string directory)
    {
        if (!IsLocalServer(dataSource) || string.IsNullOrWhiteSpace(directory))
            return "Bilinmiyor";

        try
        {
            if (!Directory.Exists(directory))
                return "Bilinmiyor";

            var root = Path.GetPathRoot(Path.GetFullPath(directory));
            if (string.IsNullOrWhiteSpace(root))
                return "Bilinmiyor";

            var drive = new DriveInfo(root);
            if (!drive.IsReady)
                return "Bilinmiyor";

            var megabytes = drive.AvailableFreeSpace / 1024d / 1024d;
            return $"{megabytes:0} MB boş";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return "Bilinmiyor";
        }
    }

    public static string FolderAccess(string dataSource, string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return "Tanımlı değil";
        if (!IsLocalServer(dataSource))
            return "Bilinmiyor";

        try
        {
            return Directory.Exists(directory) ? "Erişilebilir" : "Klasör bulunamadı";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return "Bilinmiyor";
        }
    }

    public static bool IsLocalServer(string? dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource))
            return false;

        var host = dataSource.Trim();
        var slash = host.IndexOf('\\');
        if (slash >= 0)
            host = host[..slash];
        var comma = host.IndexOf(',');
        if (comma >= 0)
            host = host[..comma];
        host = host.Trim();
        return host.Equals(".", StringComparison.OrdinalIgnoreCase)
            || host.Equals("(local)", StringComparison.OrdinalIgnoreCase)
            || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }
}
