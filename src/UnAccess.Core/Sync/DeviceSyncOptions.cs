namespace UnAccess.Core.Sync;

public sealed class DeviceSyncOptions
{
    public int BatchSize { get; set; } = 50;
    public int MaxAttempts { get; set; } = 5;
    public TimeSpan StaleProcessing { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan[] RetryDelays { get; set; } =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15)
    ];

    public TimeSpan DelayForAttempt(int attemptCount)
    {
        if (RetryDelays.Length == 0)
            return TimeSpan.FromSeconds(10);

        var index = attemptCount - 1;
        if (index < 0)
            index = 0;
        if (index >= RetryDelays.Length)
            index = RetryDelays.Length - 1;
        return RetryDelays[index];
    }
}
