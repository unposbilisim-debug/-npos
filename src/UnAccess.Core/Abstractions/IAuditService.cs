using UnAccess.Core.Models;

namespace UnAccess.Core.Abstractions;

public interface IAuditService
{
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken);
}
