using Microsoft.Extensions.Configuration;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Exceptions;

namespace UnAccess.Data.Infrastructure;

internal sealed class HospitalClock : IHospitalClock
{
    private readonly object _sync = new();
    private TimeZoneInfo _zone;

    public HospitalClock(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _zone = Resolve(configuration["Hospital:TimeZoneId"]);
    }

    public TimeZoneInfo Zone
    {
        get
        {
            lock (_sync)
                return _zone;
        }
    }

    public void ApplyTimeZone(string timeZoneId)
    {
        var zone = Resolve(timeZoneId);
        lock (_sync)
            _zone = zone;
    }

    public DateTime ToHospitalLocal(DateTime evaluationTime)
    {
        if (evaluationTime.Kind == DateTimeKind.Unspecified)
            return evaluationTime;

        var utc = evaluationTime.Kind == DateTimeKind.Utc
            ? DateTime.SpecifyKind(evaluationTime, DateTimeKind.Utc)
            : evaluationTime.ToUniversalTime();
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(utc, Zone), DateTimeKind.Unspecified);
    }

    public DateTime ToUtc(DateTime hospitalLocal)
    {
        var unspecified = DateTime.SpecifyKind(hospitalLocal, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, Zone);
    }

    private static TimeZoneInfo Resolve(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            id = "Europe/Istanbul";

        var trimmed = id.Trim();
        if (TimeZoneLookup.TryFind(trimmed, out var zone))
            return zone;

        throw new UnAccessException($"Hastane saat dilimi bulunamadı: {trimmed}");
    }
}
