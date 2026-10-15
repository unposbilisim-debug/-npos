namespace UnAccess.Core.Entities;

public sealed class Door
{
    public int Id { get; set; }
    public int ZoneId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int? ControllerId { get; set; }
    public int ChannelNumber { get; set; }
    public string? EntryReaderName { get; set; }
    public string? ExitReaderName { get; set; }
    public bool HasExitReader { get; set; }
    public bool IsCritical { get; set; }
    public bool IsActive { get; set; } = true;

    public Zone Zone { get; set; } = null!;
    public Controller? Controller { get; set; }
}
