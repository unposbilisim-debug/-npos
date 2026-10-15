using Microsoft.EntityFrameworkCore;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Models;
using UnAccess.Core.Security;

namespace UnAccess.Data.Services;

internal sealed class AuditQueryService : IAuditQueryService
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly IAccessGuard _guard;

    public AuditQueryService(IDbContextFactory<UnAccessDbContext> factory, IAccessGuard guard)
    {
        _factory = factory;
        _guard = guard;
    }

    public async Task<ReportPage<AuditLogRow>> SearchAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        _guard.Require(Permissions.AuditView);
        var page = ReportPaging.NormalizePage(query.Page);
        var size = ReportPaging.NormalizeSize(query.PageSize);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = Filter(db, query);
        var total = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(item => new AuditLogRow
            {
                Id = item.Id,
                CreatedAtUtc = item.CreatedAt,
                UserName = item.UserId == null
                    ? ""
                    : db.Users.Where(user => user.Id == item.UserId).Select(user => user.FullName).FirstOrDefault() ?? "",
                Action = item.Action,
                EntityType = item.EntityType,
                Description = item.Description,
                ComputerName = item.ComputerName
            })
            .ToListAsync(cancellationToken);
        return new ReportPage<AuditLogRow> { Items = items, Page = page, PageSize = size, TotalCount = total };
    }

    public async Task<AuditLogDetail?> GetAsync(int id, CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.AuditView);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.AuditLogs.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new AuditLogDetail
            {
                Id = item.Id,
                OldValue = item.OldValue,
                NewValue = item.NewValue,
                Description = item.Description
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LookupItem>> ListUsersAsync(CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.AuditView);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.Users.AsNoTracking()
            .OrderBy(user => user.FullName)
            .Select(user => new LookupItem { Id = user.Id, Name = user.FullName, Code = user.Username, IsActive = user.IsActive })
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<Core.Entities.AuditLog> Filter(UnAccessDbContext db, AuditQuery query)
    {
        var rows = db.AuditLogs.AsNoTracking();
        if (query.FromUtc is DateTime from)
            rows = rows.Where(item => item.CreatedAt >= from);
        if (query.ToUtc is DateTime to)
            rows = rows.Where(item => item.CreatedAt < to);
        if (query.UserId is int userId)
            rows = rows.Where(item => item.UserId == userId);
        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            rows = rows.Where(item => item.Action.Contains(action));
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            var entity = query.EntityType.Trim();
            rows = rows.Where(item => item.EntityType.Contains(entity));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(item => item.Description.Contains(search) || (item.EntityId != null && item.EntityId.Contains(search)));
        }

        return rows;
    }
}
