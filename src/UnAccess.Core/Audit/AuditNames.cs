namespace UnAccess.Core.Audit;

public static class AuditActions
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Activated = "Activated";
    public const string Deactivated = "Deactivated";
    public const string StatusChanged = "StatusChanged";
    public const string FullSyncRequested = "FullSyncRequested";
    public const string RetryRequested = "RetryRequested";
}

public static class AuditEntities
{
    public const string Personnel = "Personnel";
    public const string Card = "Card";
    public const string Building = "Building";
    public const string Floor = "Floor";
    public const string Zone = "Zone";
    public const string Door = "Door";
    public const string AccessGroup = "AccessGroup";
    public const string PersonnelAccessGroup = "PersonnelAccessGroup";
    public const string AccessGroupZone = "AccessGroupZone";
    public const string AccessGroupDoor = "AccessGroupDoor";
    public const string PersonnelAccessOverride = "PersonnelAccessOverride";
    public const string TimeSchedule = "TimeSchedule";
    public const string DeviceSync = "DeviceSync";
    public const string User = "User";
    public const string SystemSetting = "SystemSetting";
}
