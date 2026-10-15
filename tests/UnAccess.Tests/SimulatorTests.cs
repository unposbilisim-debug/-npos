using UnAccess.Devices;
using UnAccess.Simulator;

namespace UnAccess.Tests;

public class SimulatorTests
{
    [Fact]
    public async Task Simulator_runs_without_database_or_vendor_sdk()
    {
        var controller = new DahuaSimulatorController();
        var disconnected = await controller.GetStatusAsync(CancellationToken.None);
        Assert.False(disconnected.IsConnected);
        await Assert.ThrowsAsync<AccessControllerException>(() => controller.OpenDoorAsync(1, CancellationToken.None));
        await Assert.ThrowsAsync<AccessControllerException>(() =>
            controller.ConnectAsync(new ControllerConnectionInfo { Host = " ", Port = 15050 }, CancellationToken.None));

        await controller.ConnectAsync(
            new ControllerConnectionInfo { Host = "127.0.0.1", Port = 15050, ControllerCode = "SIM-ASC-01" },
            CancellationToken.None);

        var doors = await controller.GetDoorsAsync(CancellationToken.None);
        Assert.Contains(doors, door => door.ChannelNumber == 1);
        await controller.OpenDoorAsync(1, CancellationToken.None);
        Assert.Equal(1, controller.LastOpenedChannel);
        Assert.NotNull(controller.LastOpenedAtUtc);

        await controller.AddOrUpdateCardAsync(new ControllerCardRequest
        {
            CardNumber = "80001022",
            PersonnelNumber = "10021",
            ValidFrom = new DateOnly(2026, 1, 1)
        }, CancellationToken.None);
        await controller.RemoveCardAsync("80001022", CancellationToken.None);
        await controller.RemoveCardAsync("80001022", CancellationToken.None);

        var access = new ControllerCardAccess
        {
            CardNumber = "80001022",
            Channels = [new ControllerChannelAccess { ChannelNumber = 1, TimeScheduleId = 7, TimeScheduleCode = "7_24" }]
        };
        await controller.ReplaceCardAccessAsync([access], CancellationToken.None);
        await controller.ReplaceCardAccessAsync([access], CancellationToken.None);
        var snapshot = controller.GetSnapshot();
        Assert.Equal(["80001022"], snapshot.Cards.Select(card => card.CardNumber).ToArray());
        Assert.Equal([1], snapshot.Cards[0].Channels.ToArray());

        controller.SetOffline(true);
        await Assert.ThrowsAsync<AccessControllerException>(() => controller.OpenDoorAsync(1, CancellationToken.None));
        controller.SetOffline(false);
        await controller.ConnectAsync(
            new ControllerConnectionInfo { Host = "127.0.0.1", Port = 15050, ControllerCode = "SIM-ASC-01" },
            CancellationToken.None);

        var sink = new RecordingSink();
        await controller.StartEventListenerAsync(sink, CancellationToken.None);
        await controller.RaiseCardPresentedAsync("80001022", 1, CancellationToken.None);
        await controller.RaiseAccessGrantedAsync("80001022", 1, CancellationToken.None);
        await controller.RaiseAccessDeniedAsync("00000000", 2, "Yetki yok", CancellationToken.None);

        Assert.Equal(AccessEventKind.CardPresented, sink.Events[0].Kind);
        Assert.Equal(AccessEventKind.AccessGranted, sink.Events[1].Kind);
        Assert.Equal(AccessEventKind.AccessDenied, sink.Events[2].Kind);
        Assert.Equal("Yetki yok", sink.Events[2].Reason);
        Assert.Equal(DateTimeKind.Utc, sink.Events[0].OccurredAtUtc.Kind);

        await controller.DisconnectAsync(CancellationToken.None);
        var status = await controller.GetStatusAsync(CancellationToken.None);
        Assert.False(status.IsConnected);
        await Assert.ThrowsAsync<AccessControllerException>(() => controller.OpenDoorAsync(1, CancellationToken.None));
    }

    private sealed class RecordingSink : IAccessEventSink
    {
        public List<AccessEvent> Events { get; } = [];

        public Task OnAccessEventAsync(AccessEvent accessEvent, CancellationToken cancellationToken)
        {
            Events.Add(accessEvent);
            return Task.CompletedTask;
        }
    }
}
