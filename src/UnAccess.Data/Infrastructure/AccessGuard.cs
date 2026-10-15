using UnAccess.Core.Abstractions;
using UnAccess.Core.Exceptions;

namespace UnAccess.Data.Infrastructure;

internal sealed class AccessGuard : IAccessGuard
{
    private readonly ICurrentUser _current;

    public AccessGuard(ICurrentUser current)
    {
        _current = current;
    }

    public bool Can(string permission) =>
        _current.IsAuthenticated && _current.Permissions.Contains(permission);

    public void Require(string permission)
    {
        if (!_current.IsAuthenticated)
            return;

        if (!_current.Permissions.Contains(permission))
            throw new UnAccessException("Bu işlem için yetkiniz yok.");
    }

    public void Demand(string permission)
    {
        if (!_current.IsAuthenticated || !_current.Permissions.Contains(permission))
            throw new UnAccessException("Bu işlem için yetkiniz yok.");
    }
}
