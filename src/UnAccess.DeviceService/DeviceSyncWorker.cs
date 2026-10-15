using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Models;
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

    public DeviceSyncWorker(
        IDeviceSyncProcessor processor,
        IControllerClientFactory clients,
        IUtcClock clock,
        DeviceSyncOptions options,
        ILogger<DeviceSyncWorker> logger)
    {
        _processor = processor;
        _clients = clients;
        _clock = clock;
        _options = options;
        _logger = logger;
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
        await _processor.RecoverStaleAsync(now, cancellationToken);
        var jobs = await _processor.ClaimAsync(_options.BatchSize, now, cancellationToken);
        foreach (var job in jobs)
            await ProcessJobAsync(job, cancellationToken);
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
