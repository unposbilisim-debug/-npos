using System.Text.RegularExpressions;
using UnAccess.Core.Exceptions;

namespace UnAccess.Core.Backup;

public static class BackupMessages
{
    public const string PermissionDenied = "SQL Server kullanıcısının veritabanı yedeği alma yetkisi bulunmuyor.";
    public const string RestorePermissionDenied = "SQL Server kullanıcısının veritabanı geri yükleme yetkisi bulunmuyor.";
    public const string Succeeded = "Veritabanı yedeği başarıyla oluşturuldu.";
    public const string SafetyFailed = "Geri yükleme öncesi güvenlik yedeği alınamadığı için işlem durduruldu.";
    public const string RestoreWarning = "Bu işlem mevcut UnAccess veritabanını seçilen yedekteki duruma geri döndürecektir. Yedek tarihinden sonra oluşturulan veriler kaybolabilir.";
    public const string ConfirmationText = "GERI YUKLE";
    public const string ConfirmationRejected = "Geri yükleme onayı geçersiz.";
    public const string Busy = "Başka bir yedekleme veya geri yükleme işlemi sürüyor.";
    public const string DirectoryMissing = "Yedek klasörü tanımlı değil.";
    public const string DirectoryInvalid = "Yedek klasörü geçersiz.";
    public const string DirectoryUnverified = "SQL Server'ın bu klasöre yazma yetkisi kurulum sırasında ayrıca doğrulanmalıdır.";
    public const string VerifyFailed = "Yedek dosyası doğrulanamadı. Geri yükleme başlatılmadı.";
    public const string Unexpected = "Beklenmeyen bir hata oluştu. Ayrıntılar sistem günlüğüne kaydedildi.";
    public const string UpdateNotConfigured = "Güncelleme servisi yapılandırılmadı.";
    public const string ProtectedUnreadable = "Bağlantı ayarı bu makinede çözülemedi.";
    public const string ProtectedUnsupported = "Korunan yapılandırma yalnız Windows üzerinde kullanılabilir.";
    public const string Abandoned = "İşlem tamamlanmadan kesildi.";
    public const string BackupPathFailed = "SQL Server yedek dosyasını oluşturamadı. Klasör ve servis hesabı izinlerini kontrol edin.";
    public const string RestoreFailed = "Geri yükleme tamamlanamadı. Ayrıntı sistem günlüğüne yazıldı.";
    public const string MaintenanceSync = "Bakım kilidi nedeniyle senkronizasyon turu ertelendi.";
}

public static class BackupKinds
{
    public const string Manual = "Manual";
    public const string Scheduled = "Scheduled";
    public const string PreRestoreSafety = "PreRestoreSafety";
}

public static class BackupStatuses
{
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

public static class BackupTriggers
{
    public const string User = "User";
    public const string DeviceService = "DeviceService";
}

public static class ReadinessLevels
{
    public const string Ok = "OK";
    public const string Warning = "Warning";
    public const string Error = "Error";
}

public static class BackupSchedule
{
    public static bool ShouldRun(bool enabled, TimeOnly planned, DateTime hospitalNow, DateOnly? lastScheduledSuccess)
    {
        if (!enabled)
            return false;
        if (hospitalNow.TimeOfDay < planned.ToTimeSpan())
            return false;
        return lastScheduledSuccess != DateOnly.FromDateTime(hospitalNow);
    }
}

public static class BackupFiles
{
    private static readonly Regex ScheduledName = new(
        "^[A-Za-z_][A-Za-z0-9_]*_[0-9]{8}_[0-9]{6}(_[0-9]+)?\\.bak$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Create(string databaseName, DateTime hospitalLocal, string kind)
    {
        var stamp = hospitalLocal.ToString("yyyyMMdd_HHmmss");
        var middle = string.Equals(kind, BackupKinds.PreRestoreSafety, StringComparison.Ordinal) ? "_PreRestore_" : "_";
        return databaseName + middle + stamp + ".bak";
    }

    public static bool IsScheduledFile(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("_PreRestore_", StringComparison.Ordinal))
            return false;
        return ScheduledName.IsMatch(fileName);
    }

    public static string Join(string directory, string fileName)
    {
        var dir = directory.Trim().TrimEnd('/', '\\');
        var separator = dir.Contains('\\') || (dir.Length >= 2 && dir[1] == ':') ? '\\' : '/';
        return dir + separator + fileName;
    }

    public static bool SameDirectory(string? left, string? right)
    {
        var a = Normalize(left);
        var b = Normalize(right);
        return a.Length > 0 && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    public static string RequireDirectory(string? path)
    {
        var text = (path ?? "").Trim();
        if (text.Length == 0)
            throw new UnAccessException(BackupMessages.DirectoryMissing);
        if (text.Length > 500 || text.Any(char.IsControl) || text.Contains('\'') || text.Contains('"') || text.Contains(';'))
            throw new UnAccessException(BackupMessages.DirectoryInvalid);
        var windows = text.Length >= 3 && char.IsLetter(text[0]) && text[1] == ':' && (text[2] == '\\' || text[2] == '/');
        var unc = text.StartsWith("\\\\", StringComparison.Ordinal) || text.StartsWith("//", StringComparison.Ordinal);
        var unix = text.StartsWith('/');
        if (!windows && !unc && !unix)
            throw new UnAccessException(BackupMessages.DirectoryInvalid);
        return text;
    }

    public static string? ResolveLocalDeletePath(string directory, string fileName)
    {
        if (!IsScheduledFile(fileName) || !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
            return null;

        string root;
        string full;
        try
        {
            root = Path.GetFullPath(directory);
            full = Path.GetFullPath(Path.Combine(root, fileName));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;
        if (!string.Equals(Path.GetFileName(full), fileName, StringComparison.OrdinalIgnoreCase))
            return null;
        return full;
    }

    private static string Normalize(string? path)
    {
        var text = (path ?? "").Trim().TrimEnd('/', '\\');
        return text.Replace('\\', '/');
    }
}

public sealed class BackupRetentionCandidate
{
    public string BackupType { get; init; } = "";
    public string Status { get; init; } = "";
    public string FileName { get; init; } = "";
    public string BackupDirectory { get; init; } = "";
    public DateTime? CompletedAtUtc { get; init; }
}

public static class BackupRetention
{
    public static bool ShouldDelete(BackupRetentionCandidate row, string configuredDirectory, DateTime utcNow, int retentionDays)
    {
        if (!string.Equals(row.BackupType, BackupKinds.Scheduled, StringComparison.Ordinal))
            return false;
        if (!string.Equals(row.Status, BackupStatuses.Completed, StringComparison.Ordinal))
            return false;
        if (row.CompletedAtUtc is not DateTime completed || retentionDays < 1)
            return false;
        if (completed > utcNow.AddDays(-retentionDays))
            return false;
        if (!BackupFiles.IsScheduledFile(row.FileName))
            return false;
        return BackupFiles.SameDirectory(row.BackupDirectory, configuredDirectory);
    }
}
