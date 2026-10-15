using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Models;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Data.Services;

internal sealed class ControllerQueryService : ServiceBase, IControllerQueryService
{
    public ControllerQueryService(
        IDbContextFactory<UnAccessDbContext> factory,
        AuditWriter audit,
        ILogger<ControllerQueryService> logger)
        : base(factory, audit, logger)
    {
    }

    public Task<IReadOnlyList<ControllerListItem>> ListAsync(CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var rows = await db.Controllers.AsNoTracking()
                .OrderBy(controller => controller.Name)
                .Select(controller => new ControllerListItem
                {
                    Id = controller.Id,
                    Name = controller.Name,
                    Model = controller.Model,
                    IpAddress = controller.IpAddress,
                    Port = controller.Port,
                    SerialNumber = controller.SerialNumber,
                    IsActive = controller.IsActive,
                    LastSeenAtUtc = controller.LastSeenAt
                })
                .ToListAsync(ct);
            return (IReadOnlyList<ControllerListItem>)rows;
        }, cancellationToken);
    }
}
