using UnAccess.Core.Common;
using UnAccess.Core.Entities;
using UnAccess.Core.Models;

namespace UnAccess.Data.Infrastructure;

public sealed class AuditWriter
{
    public void Add(UnAccessDbContext db, AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(entry);

        db.AuditLogs.Add(new AuditLog
        {
            UserId = entry.UserId,
            Action = Required(entry.Action, FieldLengths.AuditAction),
            EntityType = Required(entry.EntityType, FieldLengths.AuditEntityType),
            EntityId = string.IsNullOrWhiteSpace(entry.EntityId) ? null : Required(entry.EntityId, FieldLengths.AuditEntityId),
            Description = Required(entry.Description, FieldLengths.AuditDescription),
            OldValue = Truncate(entry.OldValue, FieldLengths.AuditValue),
            NewValue = Truncate(entry.NewValue, FieldLengths.AuditValue),
            CreatedAt = DateTime.UtcNow,
            ComputerName = Required(Environment.MachineName, FieldLengths.ComputerName)
        });
    }

    private static string Required(string value, int maxLength)
    {
        var text = value.Trim();
        if (text.Length == 0)
            throw new InvalidOperationException("Denetim kaydı boş alan içeremez.");
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var text = value.Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
