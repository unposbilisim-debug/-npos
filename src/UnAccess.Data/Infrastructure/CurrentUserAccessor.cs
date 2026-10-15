using UnAccess.Core.Abstractions;
using UnAccess.Core.Models;

namespace UnAccess.Data.Infrastructure;

internal sealed class CurrentUserAccessor : ICurrentUser
{
    private readonly object _sync = new();
    private UserSession? _session;

    public bool IsAuthenticated
    {
        get { lock (_sync) return _session != null; }
    }

    public int? UserId
    {
        get { lock (_sync) return _session?.UserId; }
    }

    public string? Username
    {
        get { lock (_sync) return _session?.Username; }
    }

    public string? FullName
    {
        get { lock (_sync) return _session?.FullName; }
    }

    public bool MustChangePassword
    {
        get { lock (_sync) return _session?.MustChangePassword == true; }
    }

    public IReadOnlyCollection<string> Roles
    {
        get { lock (_sync) return _session?.Roles ?? []; }
    }

    public IReadOnlyCollection<string> Permissions
    {
        get { lock (_sync) return _session?.Permissions ?? []; }
    }

    public void SignIn(UserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        lock (_sync)
            _session = session;
    }

    public void SignOut()
    {
        lock (_sync)
            _session = null;
    }

    public void CompletePasswordChange()
    {
        lock (_sync)
        {
            if (_session == null)
                return;

            _session = new UserSession
            {
                UserId = _session.UserId,
                Username = _session.Username,
                FullName = _session.FullName,
                MustChangePassword = false,
                Roles = _session.Roles,
                Permissions = _session.Permissions
            };
        }
    }
}
