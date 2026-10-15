using UnAccess.Core.Enums;

namespace UnAccess.Core.Entities;

public sealed class DeviceSyncQueue
{
    public long Id { get; set; }
    public string OperationId { get; set; } = "";
    public int ControllerId { get; set; }
    public string OperationType { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Payload { get; set; } = "";
    public DeviceSyncStatus Status { get; set; } = DeviceSyncStatus.Pending;
    public int Priority { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? LastError { get; set; }

    public Controller Controller { get; set; } = null!;
}
