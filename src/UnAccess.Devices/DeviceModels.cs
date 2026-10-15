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
