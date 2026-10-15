using UnAccess.Core.Common;

namespace UnAccess.Core.Entities;

public sealed class AccessGroup : IHasTimestamps
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
