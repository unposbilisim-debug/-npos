using UnAccess.Devices;

namespace UnAccess.Simulator;

public sealed class DahuaSimulatorController : IAccessController
{
    private readonly object _sync = new();
    private readonly Dictionary<string, ControllerCardRequest> _cards = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ControllerDoorInfo> _doors;
    private bool _connected;
    private IAccessEventSink? _sink;
    private int? _lastOpenedChannel;
    private DateTime? _lastOpenedAtUtc;
    private DateTime? _lastSeenAtUtc;

    public DahuaSimulatorController()
    {
        _doors =
        [
            new ControllerDoorInfo { ChannelNumber = 1, Name = "Sim Kanal 1" },
            new ControllerDoorInfo { ChannelNumber = 2, Name = "Sim Kanal 2" }
        ];
    }

    public bool IsConnected
    {
        get { lock (_sync) return _connected; }
    }

    public int? LastOpenedChannel
    {
        get { lock (_sync) return _lastOpenedChannel; }
    }

    public DateTime? LastOpenedAtUtc
    {
        get { lock (_sync) return _lastOpenedAtUtc; }
    }

    public Task ConnectAsync(ControllerConnectionInfo connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(connection.Host))
            throw new AccessControllerException("Kontrolör adresi boş olamaz.");
        if (connection.Port is < 1 or > 65535)
            throw new AccessControllerException("Kontrolör portu 1 ile 65535 arasında olmalıdır.");

        lock (_sync)
        {
            _connected = true;
            _lastSeenAtUtc = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _connected = false;
            _sink = null;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ControllerDoorInfo>> GetDoorsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureConnected();
            _lastSeenAtUtc = DateTime.UtcNow;
            return Task.FromResult<IReadOnlyList<ControllerDoorInfo>>(_doors.ToList());
        }
    }

    public Task AddOrUpdateCardAsync(ControllerCardRequest card, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(card);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(card.CardNumber))
            throw new AccessControllerException("Kart numarası boş olamaz.");

        lock (_sync)
        {
            EnsureConnected();
            _cards[card.CardNumber.Trim()] = card;
            _lastSeenAtUtc = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task RemoveCardAsync(string cardNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(cardNumber))
            throw new AccessControllerException("Kart numarası boş olamaz.");

        lock (_sync)
        {
            EnsureConnected();
            if (!_cards.Remove(cardNumber.Trim()))
                throw new AccessControllerException("Kart simülatörde kayıtlı değil.");
            _lastSeenAtUtc = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task OpenDoorAsync(int channelNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureConnected();
            if (_doors.All(d => d.ChannelNumber != channelNumber))
                throw new AccessControllerException("Kapı kanalı simülatörde yok.");

            _lastOpenedChannel = channelNumber;
            _lastOpenedAtUtc = DateTime.UtcNow;
            _lastSeenAtUtc = _lastOpenedAtUtc;
        }

        return Task.CompletedTask;
    }

    public Task<ControllerStatusInfo> GetStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return Task.FromResult(new ControllerStatusInfo
            {
                IsConnected = _connected,
                LastSeenAtUtc = _lastSeenAtUtc,
                Detail = _connected ? "Simülatör bağlı." : "Simülatör bağlı değil."
            });
        }
    }

    public Task StartEventListenerAsync(IAccessEventSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureConnected();
            _sink = sink;
            _lastSeenAtUtc = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task StopEventListenerAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _sink = null;
        }

        return Task.CompletedTask;
    }

    public Task RaiseCardPresentedAsync(string cardNumber, int channelNumber, CancellationToken cancellationToken) =>
        PublishAsync(AccessEventKind.CardPresented, cardNumber, channelNumber, null, cancellationToken);

    public Task RaiseAccessGrantedAsync(string cardNumber, int channelNumber, CancellationToken cancellationToken) =>
        PublishAsync(AccessEventKind.AccessGranted, cardNumber, channelNumber, null, cancellationToken);

    public Task RaiseAccessDeniedAsync(string cardNumber, int channelNumber, string reason, CancellationToken cancellationToken) =>
        PublishAsync(AccessEventKind.AccessDenied, cardNumber, channelNumber, reason, cancellationToken);

    private async Task PublishAsync(
        AccessEventKind kind,
        string cardNumber,
        int channelNumber,
        string? reason,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(cardNumber))
            throw new AccessControllerException("Kart numarası boş olamaz.");

        IAccessEventSink sink;
        lock (_sync)
        {
            EnsureConnected();
            if (_sink == null)
                throw new AccessControllerException("Olay dinleyicisi başlatılmamış.");
            if (_doors.All(d => d.ChannelNumber != channelNumber))
                throw new AccessControllerException("Kapı kanalı simülatörde yok.");

            sink = _sink;
            _lastSeenAtUtc = DateTime.UtcNow;
        }

        var accessEvent = new AccessEvent
        {
            Kind = kind,
            CardNumber = cardNumber.Trim(),
            ChannelNumber = channelNumber,
            OccurredAtUtc = DateTime.UtcNow,
            Reason = reason
        };

        await sink.OnAccessEventAsync(accessEvent, cancellationToken).ConfigureAwait(false);
    }

    private void EnsureConnected()
    {
        if (!_connected)
            throw new AccessControllerException("Kontrolör bağlantısı yok.");
    }
}
