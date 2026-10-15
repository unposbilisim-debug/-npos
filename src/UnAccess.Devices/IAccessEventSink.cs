namespace UnAccess.Devices;

public interface IAccessEventSink
{
    Task OnAccessEventAsync(AccessEvent accessEvent, CancellationToken cancellationToken);
}
