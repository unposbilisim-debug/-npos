namespace UnAccess.Core.Models;

public sealed class AuditEntry
{
    public int? UserId { get; init; }
    public required string Action { get; init; }
    public required string EntityType { get; init; }
    public string? EntityId { get; init; }
    public required string Description { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}
