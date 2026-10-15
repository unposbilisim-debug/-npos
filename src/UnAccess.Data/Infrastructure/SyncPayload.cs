using System.Text.Json;

namespace UnAccess.Data.Infrastructure;

internal static class SyncPayload
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string Card(int cardId, string cardNumber, int personnelId) =>
        JsonSerializer.Serialize(new CardBody(cardId, cardNumber, personnelId), Options);

    public static string Personnel(int personnelId) =>
        JsonSerializer.Serialize(new PersonnelBody(personnelId), Options);

    public static string Controller(int controllerId) =>
        JsonSerializer.Serialize(new ControllerBody(controllerId), Options);

    public static CardBody ReadCard(string payload) =>
        JsonSerializer.Deserialize<CardBody>(payload, Options) ?? new CardBody(0, "", 0);

    public static PersonnelBody ReadPersonnel(string payload) =>
        JsonSerializer.Deserialize<PersonnelBody>(payload, Options) ?? new PersonnelBody(0);

    public static string CardNumber(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return "";
        try
        {
            return ReadCard(payload).CardNumber;
        }
        catch (JsonException)
        {
            return "";
        }
    }

    internal sealed record CardBody(int CardId, string CardNumber, int PersonnelId);

    internal sealed record PersonnelBody(int PersonnelId);

    internal sealed record ControllerBody(int ControllerId);
}
