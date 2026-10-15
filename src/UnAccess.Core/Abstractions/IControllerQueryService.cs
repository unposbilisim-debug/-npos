using UnAccess.Core.Models;

namespace UnAccess.Core.Abstractions;

public interface IControllerQueryService
{
    Task<IReadOnlyList<ControllerListItem>> ListAsync(CancellationToken cancellationToken);
}
