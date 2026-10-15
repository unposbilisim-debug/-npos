namespace UnAccess.Core.Audit;

public static class AuditActions
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Activated = "Activated";
    public const string Deactivated = "Deactivated";
    public const string StatusChanged = "StatusChanged";
}

public static class AuditEntities
{
    public const string Personnel = "Personnel";
    public const string Card = "Card";
    public const string Building = "Building";
    public const string Floor = "Floor";
    public const string Zone = "Zone";
    public const string Door = "Door";
}
