namespace UnAccess.Core.Entities;

public sealed class RestoreHistory
{
    public int Id { get; set; }
    public string BackupFileName { get; set; } = "";
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string Status { get; set; } = "";
    public int? TriggeredByUserId { get; set; }
    public string? SafetyBackupFileName { get; set; }
    public string? ErrorMessage { get; set; }
}
