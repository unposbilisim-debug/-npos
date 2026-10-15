using UnAccess.Core.Enums;
using UnAccess.Core.Models;

namespace UnAccess.Core.Abstractions;

public interface ICardService
{
    Task<IReadOnlyList<CardListItem>> SearchAsync(CardSearchCriteria criteria, CancellationToken cancellationToken);
    Task<IReadOnlyList<CardListItem>> GetByPersonnelAsync(int personnelId, CancellationToken cancellationToken);
    Task<CardListItem> CreateAsync(CardEditModel model, CancellationToken cancellationToken);
    Task<CardListItem> SetStatusAsync(int id, CardStatus status, CancellationToken cancellationToken);
    Task<IReadOnlyList<PersonnelOption>> GetPersonnelOptionsAsync(CancellationToken cancellationToken);
}
