namespace UnAccess.Devices;

public sealed class ControllerConnectionInfo
{
    public required string Host { get; init; }
    public required int Port { get; init; }
    public string? ControllerCode { get; init; }
}

public sealed class ControllerDoorInfo
{
    public required int ChannelNumber { get; init; }
    public required string Name { get; init; }
}

public sealed class ControllerCardRequest
{
    public required string CardNumber { get; init; }
    public string? PersonnelNumber { get; init; }
    public DateOnly? ValidFrom { get; init; }
    public DateOnly? ValidUntil { get; init; }
}

public readonly record struct ControllerTimeWindow(int DayOfWeek, TimeOnly Start, TimeOnly End);

public sealed class ControllerChannelAccess
{
    public required int ChannelNumber { get; init; }
    public int? TimeScheduleId { get; init; }
    public string? TimeScheduleCode { get; init; }
    public IReadOnlyList<ControllerTimeWindow> Windows { get; init; } = [];
}

public sealed class ControllerCardAccess
{
    public required string CardNumber { get; init; }
    public string? PersonnelNumber { get; init; }
    public DateOnly? ValidFrom { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public IReadOnlyList<ControllerChannelAccess> Channels { get; init; } = [];
}

public sealed class SimulatorChannelSnapshot
{
    public int ChannelNumber { get; init; }
    public int? TimeScheduleId { get; init; }
    public string? TimeScheduleCode { get; init; }
}

public sealed class SimulatorCardSnapshot
{
    public string CardNumber { get; init; } = "";
    public IReadOnlyList<int> Channels { get; init; } = [];
    public IReadOnlyList<SimulatorChannelSnapshot> Schedules { get; init; } = [];
}

public sealed class SimulatorSnapshot
{
    public bool IsOnline { get; init; }
    public bool IsConnected { get; init; }
    public IReadOnlyList<SimulatorCardSnapshot> Cards { get; init; } = [];
}

public sealed class ControllerStatusInfo
{
    public required bool IsConnected { get; init; }
    public DateTime? LastSeenAtUtc { get; init; }
    public string? Detail { get; init; }
}

public sealed class AccessEvent
{
    public required AccessEventKind Kind { get; init; }
    public required string CardNumber { get; init; }
    public required int ChannelNumber { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string? Reason { get; init; }
}
