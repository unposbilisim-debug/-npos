using UnAccess.Core.Enums;

namespace UnAccess.Core.Models;

public sealed class CardSearchCriteria
{
    public string? Text { get; init; }
}

public sealed class CardEditModel
{
    public int PersonnelId { get; set; }
    public string CardNumber { get; set; } = "";
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public string? Description { get; set; }
}

public sealed class CardListItem
{
    public int Id { get; init; }
    public string CardNumber { get; init; } = "";
    public int PersonnelId { get; init; }
    public string PersonnelName { get; init; } = "";
    public string PersonnelNumber { get; init; } = "";
    public string DepartmentName { get; init; } = "";
    public CardStatus Status { get; init; }
    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidUntil { get; init; }
}
