using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Models;

namespace UnAccess.Data.Infrastructure;

internal abstract class ServiceBase
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly AuditWriter _audit;
    private readonly ILogger _logger;

    protected ServiceBase(IDbContextFactory<UnAccessDbContext> factory, AuditWriter audit, ILogger logger)
    {
        _factory = factory;
        _audit = audit;
        _logger = logger;
    }

    protected async Task<T> QueryAsync<T>(
        Func<UnAccessDbContext, CancellationToken, Task<T>> query,
        CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await query(db, cancellationToken);
    }

    protected async Task<T> SaveWithAuditAsync<T>(
        Func<UnAccessDbContext, CancellationToken, Task<T>> mutate,
        Func<T, AuditEntry> auditFor,
        CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await mutate(db, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            _audit.Add(db, auditFor(result));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateException exception)
        {
            throw DbExceptionMapper.Map(exception, _logger);
        }
    }
}
