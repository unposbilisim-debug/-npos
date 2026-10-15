namespace UnAccess.Core.Entities;

public sealed class BackupHistory
{
    public int Id { get; set; }
    public string BackupType { get; set; } = "";
    public string FileName { get; set; } = "";
    public string BackupDirectory { get; set; } = "";
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string Status { get; set; } = "";
    public long? FileSizeBytes { get; set; }
    public string TriggeredBy { get; set; } = "";
    public int? TriggeredByUserId { get; set; }
    public string? ErrorMessage { get; set; }
}
