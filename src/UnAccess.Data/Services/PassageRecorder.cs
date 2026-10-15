using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Entities;
using UnAccess.Core.Enums;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Data.Services;

internal sealed class PassageRecorder : IPassageRecorder
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly AccessDecisionService _decisions;
    private readonly IHospitalClock _clock;
    private readonly ILogger<PassageRecorder> _logger;

    public PassageRecorder(
        IDbContextFactory<UnAccessDbContext> factory,
        AccessDecisionService decisions,
        IHospitalClock clock,
        ILogger<PassageRecorder> logger)
    {
        _factory = factory;
        _decisions = decisions;
        _clock = clock;
        _logger = logger;
    }

    public async Task<SimulatedPassageResult> RecordAsync(
        int personnelId,
        int doorId,
        DateTime hospitalLocal,
        CancellationToken cancellationToken)
    {
        var decision = await _decisions.EvaluateAsync(personnelId, doorId, hospitalLocal, cancellationToken);
        if (decision.Reason == "Kapı bulunamadı.")
            throw new UnAccessException("Kapı bulunamadı.");

        var occurredAtUtc = _clock.ToUtc(_clock.ToHospitalLocal(hospitalLocal));
        var eventId = Guid.NewGuid().ToString("N");
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var personnelExists = decision.PersonnelId > 0
            && await db.Personnel.AnyAsync(item => item.Id == decision.PersonnelId, cancellationToken);
        db.AccessEvents.Add(new AccessEvent
        {
            EventId = eventId,
            ControllerId = decision.ControllerId,
            DoorId = decision.DoorId,
            PersonnelId = personnelExists ? decision.PersonnelId : null,
            CardId = decision.CardId,
            CardNumber = decision.CardNumber,
            EventType = AccessEventTypes.CardRead,
            Decision = decision.IsAllowed ? nameof(AccessDecisionKind.Granted) : nameof(AccessDecisionKind.Denied),
            Reason = decision.Reason,
            OccurredAtUtc = occurredAtUtc,
            ReceivedAtUtc = DateTime.UtcNow,
            Source = AccessEventSources.Simulator,
            RawData = $"PersonnelId={personnelId};DoorId={doorId};Local={_clock.ToHospitalLocal(hospitalLocal):yyyy-MM-dd HH:mm}"
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw DbExceptionMapper.Map(exception, _logger);
        }

        return new SimulatedPassageResult
        {
            Decision = decision,
            EventId = eventId
        };
    }
}

internal sealed class AccessEventQueryService : IAccessEventQueryService
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly IHospitalClock _clock;

    public AccessEventQueryService(IDbContextFactory<UnAccessDbContext> factory, IHospitalClock clock)
    {
        _factory = factory;
        _clock = clock;
    }

    public async Task<IReadOnlyList<AccessEventListItem>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        var limit = take is < 1 or > 100 ? 100 : take;
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await (
            from accessEvent in db.AccessEvents.AsNoTracking()
            join door in db.Doors.AsNoTracking() on accessEvent.DoorId equals door.Id
            join zone in db.Zones.AsNoTracking() on door.ZoneId equals zone.Id
            join person in db.Personnel.AsNoTracking() on accessEvent.PersonnelId equals person.Id into people
            from person in people.DefaultIfEmpty()
            orderby accessEvent.OccurredAtUtc descending, accessEvent.Id descending
            select new
            {
                accessEvent.Id,
                accessEvent.OccurredAtUtc,
                PersonnelName = person == null ? "" : person.FirstName + " " + person.LastName,
                accessEvent.CardNumber,
                DoorName = door.Name,
                ZoneName = zone.Name,
                accessEvent.Decision,
                accessEvent.Reason
            }).Take(limit).ToListAsync(cancellationToken);

        return rows.Select(row => new AccessEventListItem
        {
            Id = row.Id,
            TimeText = _clock.ToHospitalLocal(DateTime.SpecifyKind(row.OccurredAtUtc, DateTimeKind.Utc)).ToString("dd.MM.yyyy HH:mm:ss"),
            PersonnelName = row.PersonnelName,
            CardNumber = row.CardNumber,
            DoorName = row.DoorName,
            ZoneName = row.ZoneName,
            ResultText = row.Decision == nameof(AccessDecisionKind.Granted) ? "ONAYLANDI" : row.Decision == nameof(AccessDecisionKind.Denied) ? "REDDEDİLDİ" : "BİLİNMİYOR",
            Reason = row.Reason,
            IsGranted = row.Decision == nameof(AccessDecisionKind.Granted)
        }).ToList();
    }
}
