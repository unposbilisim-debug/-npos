namespace UnAccess.Core.Models;

public sealed class LookupItem
{
    public int Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public bool IsActive { get; init; }
    public string Display => string.IsNullOrWhiteSpace(Code) ? Name : $"{Code} - {Name}";
}

public sealed class PersonnelOption
{
    public int Id { get; init; }
    public string PersonnelNumber { get; init; } = "";
    public string FullName { get; init; } = "";
    public string Display => $"{PersonnelNumber} - {FullName}";
}

public sealed class FloorOption
{
    public int Id { get; init; }
    public int BuildingId { get; init; }
    public string Display { get; init; } = "";
    public bool IsActive { get; init; }
}

public sealed class ZoneOption
{
    public int Id { get; init; }
    public int FloorId { get; init; }
    public string Display { get; init; } = "";
    public bool IsActive { get; init; }
}

public sealed class ControllerOption
{
    public int Id { get; init; }
    public string Display { get; init; } = "";
    public bool IsActive { get; init; }
}
