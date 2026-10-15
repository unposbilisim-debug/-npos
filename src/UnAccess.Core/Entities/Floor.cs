namespace UnAccess.Core.Entities;

public sealed class Floor
{
    public int Id { get; set; }
    public int BuildingId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int FloorNumber { get; set; }
    public bool IsActive { get; set; } = true;

    public Building Building { get; set; } = null!;
}
