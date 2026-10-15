namespace UnAccess.Devices;

public interface IAccessController
{
    Task ConnectAsync(ControllerConnectionInfo connection, CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ControllerDoorInfo>> GetDoorsAsync(CancellationToken cancellationToken);
    Task AddOrUpdateCardAsync(ControllerCardRequest card, CancellationToken cancellationToken);
    Task UpsertCardAccessAsync(ControllerCardAccess access, CancellationToken cancellationToken);
    Task RemoveCardAsync(string cardNumber, CancellationToken cancellationToken);
    Task ReplaceCardAccessAsync(IReadOnlyList<ControllerCardAccess> cards, CancellationToken cancellationToken);
    Task OpenDoorAsync(int channelNumber, CancellationToken cancellationToken);
    Task<ControllerStatusInfo> GetStatusAsync(CancellationToken cancellationToken);
    Task StartEventListenerAsync(IAccessEventSink sink, CancellationToken cancellationToken);
    Task StopEventListenerAsync(CancellationToken cancellationToken);
}
