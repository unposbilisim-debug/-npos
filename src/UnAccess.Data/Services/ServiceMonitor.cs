using Microsoft.EntityFrameworkCore;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Common;
using UnAccess.Core.Entities;
using UnAccess.Core.Models;
using UnAccess.Core.Security;

namespace UnAccess.Data.Services;

internal sealed class ServiceMonitor : IServiceMonitor
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly IUtcClock _clock;

    public ServiceMonitor(IDbContextFactory<UnAccessDbContext> factory, IUtcClock clock)
    {
        _factory = factory;
        _clock = clock;
    }

    public async Task BeatAsync(ServicePulse pulse, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pulse);
        var name = TextRules.Required(pulse.ServiceName, "Servis", FieldLengths.ServiceName);
        var instance = TextRules.Required(pulse.InstanceId, "Örnek", FieldLengths.InstanceId);
        var version = string.IsNullOrWhiteSpace(pulse.Version) ? "1.0.0" : pulse.Version.Trim();
        if (version.Length > FieldLengths.Version)
            version = version[..FieldLengths.Version];

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var row = await db.ServiceHeartbeats.FirstOrDefaultAsync(
            item => item.ServiceName == name && item.InstanceId == instance,
            cancellationToken);
        var now = _clock.UtcNow;
        if (row == null)
        {
            db.ServiceHeartbeats.Add(new ServiceHeartbeat
            {
                ServiceName = name,
                InstanceId = instance,
                MachineName = Environment.MachineName,
                LastSeenAtUtc = now,
                StartedAtUtc = pulse.StartedAtUtc == default ? now : pulse.StartedAtUtc,
                Version = version
            });
        }
        else
        {
            row.LastSeenAtUtc = now;
            row.MachineName = Environment.MachineName;
            row.Version = version;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
