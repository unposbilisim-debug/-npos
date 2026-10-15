namespace UnAccess.Core.Entities;

public sealed class Controller
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public int Port { get; set; }
    public string? SerialNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSeenAt { get; set; }
}
