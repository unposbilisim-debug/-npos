namespace UnAccess.Core.Entities;

public sealed class ServiceHeartbeat
{
    public int Id { get; set; }
    public string ServiceName { get; set; } = "";
    public string InstanceId { get; set; } = "";
    public string MachineName { get; set; } = "";
    public DateTime LastSeenAtUtc { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public string Version { get; set; } = "";
}
