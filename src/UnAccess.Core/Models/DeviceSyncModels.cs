using UnAccess.Core.Access;

namespace UnAccess.Core.Models;

public sealed class DeviceChannelProjection
{
    public int DoorId { get; init; }
    public string DoorName { get; init; } = "";
    public int ChannelNumber { get; init; }
    public int TimeScheduleId { get; init; }
    public string TimeScheduleCode { get; init; } = "";
    public IReadOnlyList<TimeWindow> Windows { get; init; } = [];
}

public sealed class ControllerAccessProjection
{
    public int ControllerId { get; init; }
    public string ControllerCode { get; init; } = "";
    public IReadOnlyList<DeviceChannelProjection> Channels { get; init; } = [];
}

public sealed class PersonnelDeviceProjection
{
    public int PersonnelId { get; init; }
    public string PersonnelNumber { get; init; } = "";
    public int? CardId { get; init; }
    public string CardNumber { get; init; } = "";
    public DateOnly? ValidFrom { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public IReadOnlyList<ControllerAccessProjection> Controllers { get; init; } = [];
}

public sealed class DeviceSyncJob
{
    public long Id { get; init; }
    public string OperationId { get; init; } = "";
    public int ControllerId { get; init; }
    public string OperationType { get; init; } = "";
    public string EntityType { get; init; } = "";
    public string EntityId { get; init; } = "";
    public string Payload { get; init; } = "";
    public int AttemptCount { get; init; }
    public int MaxAttempts { get; init; }
}

public enum DeviceCommandKind
{
    None = 0,
    Upsert = 1,
    Remove = 2,
    Replace = 3
}

public sealed class DeviceChannelCommand
{
    public int ChannelNumber { get; init; }
    public int TimeScheduleId { get; init; }
    public string TimeScheduleCode { get; init; } = "";
    public IReadOnlyList<TimeWindow> Windows { get; init; } = [];
}

public sealed class DeviceCardCommand
{
    public string CardNumber { get; init; } = "";
    public string? PersonnelNumber { get; init; }
    public DateOnly? ValidFrom { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public IReadOnlyList<DeviceChannelCommand> Channels { get; init; } = [];
}

public sealed class DeviceCommand
{
    public bool Skip { get; init; }
    public string? SkipReason { get; init; }
    public int ControllerId { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string? ControllerCode { get; set; }
    public DeviceCommandKind Kind { get; init; }
    public DeviceCardCommand? Card { get; init; }
    public IReadOnlyList<DeviceCardCommand> Cards { get; init; } = [];
}

public sealed class DeviceSyncFilter
{
    public int? ControllerId { get; init; }
    public string? Status { get; init; }
    public string? OperationType { get; init; }
}

public sealed class DeviceSyncJobItem
{
    public long Id { get; init; }
    public string CreatedAtText { get; init; } = "";
    public string ControllerName { get; init; } = "";
    public string OperationText { get; init; } = "";
    public string EntityText { get; init; } = "";
    public string Status { get; init; } = "";
    public string StatusText { get; init; } = "";
    public int AttemptCount { get; init; }
    public string LastAttemptText { get; init; } = "";
    public string? LastError { get; init; }
}

public sealed class DeviceSyncSummary
{
    public int Pending { get; init; }
    public int Processing { get; init; }
    public int Failed { get; init; }
    public int Completed { get; init; }
}
