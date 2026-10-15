using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Backup;
using UnAccess.Core.Models;
using UnAccess.Core.Security;
using UnAccess.Core.Sync;
using UnAccess.Devices;

namespace UnAccess.DeviceService;

public sealed class DeviceSyncWorker : BackgroundService
{
    private readonly IDeviceSyncProcessor _processor;
    private readonly IControllerClientFactory _clients;
    private readonly IUtcClock _clock;
    private readonly DeviceSyncOptions _options;
    private readonly ILogger<DeviceSyncWorker> _logger;
    private readonly IRuntimeSettings? _runtime;
    private readonly IServiceMonitor? _monitor;
    private readonly IMaintenanceGate? _maintenance;
    private readonly string _instanceId = Guid.NewGuid().ToString("N");
    private readonly DateTime _startedAtUtc = DateTime.UtcNow;
    private DateTime _lastBeatUtc = DateTime.MinValue;

    public DeviceSyncWorker(
        IDeviceSyncProcessor processor,
        IControllerClientFactory clients,
        IUtcClock clock,
        DeviceSyncOptions options,
        ILogger<DeviceSyncWorker> logger,
        IRuntimeSettings? runtime = null,
        IServiceMonitor? monitor = null,
        IMaintenanceGate? maintenance = null)
    {
        _processor = processor;
        _clients = clients;
        _clock = clock;
        _options = options;
        _logger = logger;
        _runtime = runtime;
        _monitor = monitor;
        _maintenance = maintenance;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Senkronizasyon turu tamamlanamadı.");
            }

            await Task.Delay(_options.PollInterval, stoppingToken);
        }
    }

    public async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        await BeatAsync(cancellationToken);
        if (_maintenance != null)
        {
            var lease = await _maintenance.TryEnterSharedAsync(cancellationToken);
            if (lease == null)
            {
                _logger.LogInformation(BackupMessages.MaintenanceSync);
                return;
            }

            await using (lease)
            {
                await ProcessJobsAsync(now, cancellationToken);
            }

            return;
        }

        await ProcessJobsAsync(now, cancellationToken);
    }

    private async Task ProcessJobsAsync(DateTime now, CancellationToken cancellationToken)
    {
        await _processor.RecoverStaleAsync(now, cancellationToken);
        var batch = _runtime == null ? _options.BatchSize : await _runtime.DeviceSyncBatchSizeAsync(cancellationToken);
        var jobs = await _processor.ClaimAsync(batch, now, cancellationToken);
        foreach (var job in jobs)
            await ProcessJobAsync(job, cancellationToken);
    }

    private async Task BeatAsync(CancellationToken cancellationToken)
    {
        if (_monitor == null)
            return;

        var interval = _runtime == null
            ? TimeSpan.FromSeconds(SecurityDefaults.HeartbeatIntervalSeconds)
            : await _runtime.HeartbeatIntervalAsync(cancellationToken);
        var now = _clock.UtcNow;
        if (_lastBeatUtc != DateTime.MinValue && now - _lastBeatUtc < interval)
            return;

        await _monitor.BeatAsync(new ServicePulse
        {
            ServiceName = ServiceNames.DeviceSync,
            InstanceId = _instanceId,
            StartedAtUtc = _startedAtUtc,
            Version = typeof(DeviceSyncWorker).Assembly.GetName().Version?.ToString() ?? ""
        }, cancellationToken);
        _lastBeatUtc = now;
    }

    private async Task ProcessJobAsync(DeviceSyncJob job, CancellationToken cancellationToken)
    {
        try
        {
            var command = await _processor.BuildCommandAsync(job, cancellationToken);
            if (command.Skip)
            {
                await _processor.CompleteAsync(job.Id, job.ControllerId, _clock.UtcNow, cancellationToken);
                return;
            }

            var client = _clients.Create(job.ControllerId);
            await client.ConnectAsync(new ControllerConnectionInfo
            {
                Host = command.Host,
                Port = command.Port,
                ControllerCode = command.ControllerCode
            }, cancellationToken);

            switch (command.Kind)
            {
                case DeviceCommandKind.Upsert:
                    await client.UpsertCardAccessAsync(ToAccess(command.Card), cancellationToken);
                    break;
                case DeviceCommandKind.Remove:
                    await client.RemoveCardAsync(command.Card?.CardNumber ?? "", cancellationToken);
                    break;
                case DeviceCommandKind.Replace:
                    await client.ReplaceCardAccessAsync(command.Cards.Select(ToAccess).ToList(), cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException("Senkronizasyon komutu boş.");
            }

            await _processor.CompleteAsync(job.Id, job.ControllerId, _clock.UtcNow, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Senkronizasyon işi başarısız. İşlem {OperationId}, kontrolör {ControllerId}", job.OperationId, job.ControllerId);
            await _processor.FailAsync(job.Id, job.ControllerId, exception.Message, _clock.UtcNow, cancellationToken);
        }
    }

    private static ControllerCardAccess ToAccess(DeviceCardCommand? card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return new ControllerCardAccess
        {
            CardNumber = card.CardNumber,
            PersonnelNumber = card.PersonnelNumber,
            ValidFrom = card.ValidFrom,
            ValidUntil = card.ValidUntil,
            Channels = card.Channels.Select(channel => new ControllerChannelAccess
            {
                ChannelNumber = channel.ChannelNumber,
                TimeScheduleId = channel.TimeScheduleId,
                TimeScheduleCode = channel.TimeScheduleCode,
                Windows = channel.Windows.Select(window => new ControllerTimeWindow((int)window.Day, window.Start, window.End)).ToArray()
            }).ToArray()
        };
    }
}
