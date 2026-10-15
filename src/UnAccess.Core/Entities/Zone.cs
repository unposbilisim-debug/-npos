namespace UnAccess.Core.Entities;

public sealed class Zone
{
    public int Id { get; set; }
    public int FloorId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsCritical { get; set; }
    public bool IsActive { get; set; } = true;

    public Floor Floor { get; set; } = null!;
}
