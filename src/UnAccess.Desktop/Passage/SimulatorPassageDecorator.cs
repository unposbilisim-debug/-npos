using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Models;
using UnAccess.Devices;
using UnAccess.Simulator;

namespace UnAccess.Desktop.Passage;

internal sealed class SimulatorPassageDecorator : ISimulatedPassageService
{
    private readonly IPassageRecorder _recorder;
    private readonly IControllerQueryService _controllers;
    private readonly IAccessController _controller;
    private readonly ILogger<SimulatorPassageDecorator> _logger;

    public SimulatorPassageDecorator(
        IPassageRecorder recorder,
        IControllerQueryService controllers,
        IAccessController controller,
        ILogger<SimulatorPassageDecorator> logger)
    {
        _recorder = recorder;
        _controllers = controllers;
        _controller = controller;
        _logger = logger;
    }

    public async Task<SimulatedPassageResult> ReadCardAsync(
        int personnelId,
        int doorId,
        DateTime hospitalLocal,
        CancellationToken cancellationToken)
    {
        var result = await _recorder.RecordAsync(personnelId, doorId, hospitalLocal, cancellationToken);
        try
        {
            await NotifySimulatorAsync(result.Decision, cancellationToken);
        }
        catch (AccessControllerException exception)
        {
            _logger.LogWarning(exception, "Simülatör olayı gönderilemedi. Geçiş kaydı yazıldı.");
        }

        return result;
    }

    private async Task NotifySimulatorAsync(AccessDecision decision, CancellationToken cancellationToken)
    {
        var controllers = await _controllers.ListAsync(cancellationToken);
        var simulator = controllers.FirstOrDefault(item => item.IsActive && item.Model == "Simulator");
        if (simulator == null || string.IsNullOrWhiteSpace(simulator.IpAddress) || simulator.Port is < 1 or > 65535)
            return;

        var status = await _controller.GetStatusAsync(cancellationToken);
        if (!status.IsConnected)
        {
            await _controller.ConnectAsync(new ControllerConnectionInfo
            {
                Host = simulator.IpAddress,
                Port = simulator.Port,
                ControllerCode = simulator.Name
            }, cancellationToken);
        }

        if (_controller is not DahuaSimulatorController device)
            return;

        await device.StartEventListenerAsync(SilentSink.Instance, cancellationToken);
        var doors = await device.GetDoorsAsync(cancellationToken);
        var channel = doors.Any(door => door.ChannelNumber == decision.ChannelNumber)
            ? decision.ChannelNumber
            : doors.FirstOrDefault()?.ChannelNumber;
        if (channel is not int selected)
            return;

        var cardNumber = string.IsNullOrWhiteSpace(decision.CardNumber) ? "0" : decision.CardNumber;
        if (decision.IsAllowed)
            await device.RaiseAccessGrantedAsync(cardNumber, selected, cancellationToken);
        else
            await device.RaiseAccessDeniedAsync(cardNumber, selected, decision.Reason, cancellationToken);
    }

    private sealed class SilentSink : IAccessEventSink
    {
        public static readonly SilentSink Instance = new();

        public Task OnAccessEventAsync(Devices.AccessEvent accessEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
