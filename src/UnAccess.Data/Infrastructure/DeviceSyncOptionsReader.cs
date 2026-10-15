using Microsoft.Extensions.Configuration;
using UnAccess.Core.Sync;

namespace UnAccess.Data.Infrastructure;

internal static class DeviceSyncOptionsReader
{
    public static DeviceSyncOptions Read(IConfiguration configuration)
    {
        var options = new DeviceSyncOptions();
        var section = configuration.GetSection("DeviceSync");
        if (int.TryParse(section["BatchSize"], out var batch) && batch > 0)
            options.BatchSize = batch;
        if (int.TryParse(section["MaxAttempts"], out var attempts) && attempts > 0)
            options.MaxAttempts = attempts;
        if (TimeSpan.TryParse(section["StaleProcessing"], out var stale) && stale > TimeSpan.Zero)
            options.StaleProcessing = stale;
        if (TimeSpan.TryParse(section["PollInterval"], out var poll) && poll > TimeSpan.Zero)
            options.PollInterval = poll;

        var delays = new List<TimeSpan>();
        foreach (var child in section.GetSection("RetryDelays").GetChildren())
        {
            if (TimeSpan.TryParse(child.Value, out var delay) && delay >= TimeSpan.Zero)
                delays.Add(delay);
        }

        if (delays.Count > 0)
            options.RetryDelays = delays.ToArray();
        return options;
    }
}
