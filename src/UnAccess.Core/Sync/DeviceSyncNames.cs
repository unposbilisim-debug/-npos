namespace UnAccess.Core.Sync;

public static class DeviceSyncOperations
{
    public const string UpsertCard = "UpsertCard";
    public const string RemoveCard = "RemoveCard";
    public const string SyncCardPermissions = "SyncCardPermissions";
    public const string FullControllerSync = "FullControllerSync";
}

public static class DeviceSyncEntities
{
    public const string Card = "Card";
    public const string Personnel = "Personnel";
    public const string Controller = "Controller";
}

public static class DeviceSyncPriority
{
    public const int Critical = 0;
    public const int Normal = 10;
    public const int Low = 50;
}

public static class ControllerConnectionStatuses
{
    public const string Unknown = "Unknown";
    public const string Online = "Online";
    public const string Offline = "Offline";
    public const string Error = "Error";
}
