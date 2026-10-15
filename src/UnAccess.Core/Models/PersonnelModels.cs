namespace UnAccess.Core.Models;

public sealed class PersonnelSearchCriteria
{
    public string? Text { get; init; }
    public int? DepartmentId { get; init; }
    public bool? IsActive { get; init; }
}

public sealed class PersonnelEditModel
{
    public string PersonnelNumber { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public int DepartmentId { get; set; }
    public int JobTitleId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? PhotoPath { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PersonnelListItem
{
    public int Id { get; init; }
    public string PersonnelNumber { get; init; } = "";
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string DepartmentName { get; init; } = "";
    public string JobTitleName { get; init; } = "";
    public string ActiveCardNumber { get; init; } = "";
    public bool IsActive { get; init; }
}

public sealed class PersonnelCardItem
{
    public int Id { get; init; }
    public string CardNumber { get; init; } = "";
    public Enums.CardStatus Status { get; init; }
    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public string? Description { get; init; }
}

public sealed class PersonnelDetail
{
    public int Id { get; init; }
    public string PersonnelNumber { get; init; } = "";
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public int DepartmentId { get; init; }
    public string DepartmentName { get; init; } = "";
    public int JobTitleId { get; init; }
    public string JobTitleName { get; init; } = "";
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? PhotoPath { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<PersonnelCardItem> Cards { get; init; } = Array.Empty<PersonnelCardItem>();
}
