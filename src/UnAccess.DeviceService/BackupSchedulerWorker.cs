using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Security;

namespace UnAccess.DeviceService;

public sealed class BackupSchedulerWorker : BackgroundService
{
    private readonly IBackupService _backup;
    private readonly ILogger<BackupSchedulerWorker> _logger;

    public BackupSchedulerWorker(IBackupService backup, ILogger<BackupSchedulerWorker> logger)
    {
        _backup = backup;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Planlı yedekleme turu tamamlanamadı.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await _backup.RecoverStaleAsync(cancellationToken);
        var result = await _backup.CreateScheduledAsync(cancellationToken);
        if (result.FileName != null)
            await _backup.ApplyRetentionAsync(cancellationToken);
    }
}
