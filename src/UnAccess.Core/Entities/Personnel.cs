using UnAccess.Core.Common;

namespace UnAccess.Core.Entities;

public sealed class Personnel : IHasTimestamps
{
    public int Id { get; set; }
    public string PersonnelNumber { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public int DepartmentId { get; set; }
    public int JobTitleId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? PhotoPath { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Department Department { get; set; } = null!;
    public JobTitle JobTitle { get; set; } = null!;
}
