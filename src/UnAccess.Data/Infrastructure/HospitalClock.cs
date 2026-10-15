using Microsoft.Extensions.Configuration;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Exceptions;

namespace UnAccess.Data.Infrastructure;

internal sealed class HospitalClock : IHospitalClock
{
    public HospitalClock(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var id = configuration["Hospital:TimeZoneId"];
        if (string.IsNullOrWhiteSpace(id))
            id = "Europe/Istanbul";

        try
        {
            Zone = TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new UnAccessException($"Hastane saat dilimi bulunamadı: {id.Trim()}", exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new UnAccessException($"Hastane saat dilimi geçersiz: {id.Trim()}", exception);
        }
    }

    public TimeZoneInfo Zone { get; }

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
}
