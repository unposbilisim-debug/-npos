namespace UnAccess.Core.Models;

public sealed class ControllerListItem
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Model { get; init; } = "";
    public string IpAddress { get; init; } = "";
    public int Port { get; init; }
    public string? SerialNumber { get; init; }
    public bool IsActive { get; init; }
    public DateTime? LastSeenAtUtc { get; init; }
}
