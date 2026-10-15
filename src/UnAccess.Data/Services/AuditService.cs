using Microsoft.EntityFrameworkCore;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Models;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Data.Services;

internal sealed class AuditService : IAuditService
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly AuditWriter _writer;

    public AuditService(IDbContextFactory<UnAccessDbContext> factory, AuditWriter writer)
    {
        _factory = factory;
        _writer = writer;
    }

    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        _writer.Add(db, entry);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
