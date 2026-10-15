namespace UnAccess.Core.Enums;

public enum MatchedRuleType
{
    None = 0,
    PersonnelDoorOverride = 1,
    PersonnelZoneOverride = 2,
    AccessGroupDoor = 3,
    AccessGroupZone = 4
}

public enum AccessDecisionKind
{
    Unknown = 0,
    Granted = 1,
    Denied = 2
}

public static class AccessEventTypes
{
    public const string CardRead = "CardRead";
}

public static class AccessEventSources
{
    public const string Simulator = "Simulator";
    public const string Dahua = "Dahua";
}

public static class TurkishCalendar
{
    public static string Day(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Pazartesi",
        DayOfWeek.Tuesday => "Salı",
        DayOfWeek.Wednesday => "Çarşamba",
        DayOfWeek.Thursday => "Perşembe",
        DayOfWeek.Friday => "Cuma",
        DayOfWeek.Saturday => "Cumartesi",
        DayOfWeek.Sunday => "Pazar",
        _ => day.ToString()
    };
}
