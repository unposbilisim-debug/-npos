namespace UnAccess.Core.Entities;

public sealed class AccessEvent
{
    public int Id { get; set; }
    public string EventId { get; set; } = "";
    public int? ControllerId { get; set; }
    public int DoorId { get; set; }
    public int? PersonnelId { get; set; }
    public int? CardId { get; set; }
    public string CardNumber { get; set; } = "";
    public string EventType { get; set; } = "";
    public string Decision { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public string Source { get; set; } = "";
    public string? RawData { get; set; }

    public Controller? Controller { get; set; }
    public Door Door { get; set; } = null!;
    public Personnel? Personnel { get; set; }
    public Card? Card { get; set; }
}
