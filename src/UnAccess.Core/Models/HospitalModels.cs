namespace UnAccess.Core.Models;

public sealed class BuildingEditModel
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class BuildingListItem
{
    public int Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public bool IsActive { get; init; }
}

public sealed class FloorEditModel
{
    public int BuildingId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int FloorNumber { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class FloorListItem
{
    public int Id { get; init; }
    public int BuildingId { get; init; }
    public string BuildingName { get; init; } = "";
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public int FloorNumber { get; init; }
    public bool IsActive { get; init; }
}

public sealed class ZoneEditModel
{
    public int FloorId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsCritical { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ZoneListItem
{
    public int Id { get; init; }
    public int FloorId { get; init; }
    public string Location { get; init; } = "";
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public bool IsCritical { get; init; }
    public bool IsActive { get; init; }
}

public sealed class DoorEditModel
{
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
}

public sealed class DoorListItem
{
    public int Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string Location { get; init; } = "";
    public int ChannelNumber { get; init; }
    public string? ControllerName { get; init; }
    public bool IsCritical { get; init; }
    public bool IsActive { get; init; }
}
