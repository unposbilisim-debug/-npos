using UnAccess.Core.Common;
using UnAccess.Core.Enums;

namespace UnAccess.Core.Entities;

public sealed class Card : IHasTimestamps
{
    public int Id { get; set; }
    public int PersonnelId { get; set; }
    public string CardNumber { get; set; } = "";
    public CardStatus Status { get; set; } = CardStatus.Active;
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Personnel Personnel { get; set; } = null!;
}
