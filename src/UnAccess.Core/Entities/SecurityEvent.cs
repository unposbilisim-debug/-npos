namespace UnAccess.Core.Entities;

public sealed class SecurityEvent
{
    public long Id { get; set; }
    public string EventType { get; set; } = "";
    public int? UserId { get; set; }
    public string Username { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; }
    public string MachineName { get; set; } = "";
    public string? Detail { get; set; }
    public bool Success { get; set; }
}
