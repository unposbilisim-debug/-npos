using UnAccess.Core.Common;

namespace UnAccess.Core.Entities;

public sealed class PersonnelAccessGroup : IHasTimestamps
{
    public int Id { get; set; }
    public int PersonnelId { get; set; }
    public int AccessGroupId { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Personnel Personnel { get; set; } = null!;
    public AccessGroup AccessGroup { get; set; } = null!;
}
