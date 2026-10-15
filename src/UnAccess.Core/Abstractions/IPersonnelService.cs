using UnAccess.Core.Models;

namespace UnAccess.Core.Abstractions;

public interface IPersonnelService
{
    Task<IReadOnlyList<PersonnelListItem>> SearchAsync(PersonnelSearchCriteria criteria, CancellationToken cancellationToken);
    Task<PersonnelDetail?> GetAsync(int id, CancellationToken cancellationToken);
    Task<PersonnelDetail> CreateAsync(PersonnelEditModel model, CancellationToken cancellationToken);
    Task<PersonnelDetail> UpdateAsync(int id, PersonnelEditModel model, CancellationToken cancellationToken);
    Task<IReadOnlyList<LookupItem>> GetDepartmentsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<LookupItem>> GetJobTitlesAsync(CancellationToken cancellationToken);
}
