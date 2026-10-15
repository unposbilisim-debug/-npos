using UnAccess.Core.Access;
using UnAccess.Core.Enums;
using UnAccess.Core.Models;

namespace UnAccess.Tests;

public class AccessRuleResolverTests
{
    private static readonly DateTime Morning = new(2026, 10, 15, 10, 0, 0);
    private static readonly DateTime Night = new(2026, 10, 15, 22, 0, 0);

    [Fact]
    public void Personal_door_schedule_does_not_fall_back_to_the_group()
    {
        var morning = Resolve(
            Morning,
            doorOverrides: [Rule(11, true, Office())],
            groupDoors: [Rule(20, true, Full(), "CERRAH")]);
        var night = Resolve(
            Night,
            doorOverrides: [Rule(11, true, Office())],
            groupDoors: [Rule(20, true, Full(), "CERRAH")]);

        Assert.True(morning.IsAllowed);
        Assert.Equal(MatchedRuleType.PersonnelDoorOverride, morning.MatchedRuleType);
        Assert.Equal(11, morning.TimeScheduleId);

        Assert.False(night.IsAllowed);
        Assert.Equal(AccessRuleResolver.OutsideScheduleReason, night.Reason);
        Assert.Equal(MatchedRuleType.PersonnelDoorOverride, night.MatchedRuleType);
        Assert.Equal(11, night.MatchedRuleId);
        Assert.Equal(11, night.TimeScheduleId);
        Assert.Equal("KİŞİSEL İSTİSNA", night.SourceName);
    }

    [Fact]
    public void Personal_zone_schedule_does_not_fall_back_to_a_group_door()
    {
        var decision = Resolve(
            Night,
            zoneOverrides: [Rule(12, true, Office())],
            groupDoors: [Rule(21, true, Full(), "CERRAH")]);

        Assert.False(decision.IsAllowed);
        Assert.Equal(AccessRuleResolver.OutsideScheduleReason, decision.Reason);
        Assert.Equal(MatchedRuleType.PersonnelZoneOverride, decision.MatchedRuleType);
        Assert.Equal(12, decision.TimeScheduleId);
    }

    [Fact]
    public void Group_door_schedule_does_not_fall_back_to_the_group_zone()
    {
        var decision = Resolve(
            Night,
            groupDoors: [Rule(13, true, Office(), "CERRAH")],
            groupZones: [Rule(22, true, Full(), "CERRAH")]);

        Assert.False(decision.IsAllowed);
        Assert.Equal(AccessRuleResolver.OutsideScheduleReason, decision.Reason);
        Assert.Equal(MatchedRuleType.AccessGroupDoor, decision.MatchedRuleType);
        Assert.Equal(13, decision.TimeScheduleId);
        Assert.Equal("CERRAH", decision.SourceName);
    }

    [Fact]
    public void Deny_wins_when_both_schedules_match_regardless_of_rule_id()
    {
        var lowerAllow = Resolve(
            Morning,
            groupDoors:
            [
                Rule(1, true, Full(), "IZIN"),
                Rule(9, false, Full(), "YASAK")
            ]);
        var lowerDeny = Resolve(
            Morning,
            groupDoors:
            [
                Rule(2, false, Full(), "YASAK"),
                Rule(8, true, Full(), "IZIN")
            ]);

        Assert.False(lowerAllow.IsAllowed);
        Assert.Equal(9, lowerAllow.MatchedRuleId);
        Assert.Contains("YASAK", lowerAllow.Reason, StringComparison.Ordinal);
        Assert.False(lowerDeny.IsAllowed);
        Assert.Equal(2, lowerDeny.MatchedRuleId);
        Assert.Contains("YASAK", lowerDeny.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_schedule_that_matches_the_clock_competes_at_the_same_level()
    {
        var rules = new[]
        {
            Rule(1, true, Office(), "IZIN"),
            Rule(2, false, NightHours(), "YASAK")
        };

        var morning = Resolve(Morning, groupDoors: rules);
        var evening = Resolve(Night, groupDoors: rules);
        var afterMidnight = Resolve(new DateTime(2026, 10, 16, 2, 0, 0), groupDoors: rules);
        var afternoonGap = Resolve(new DateTime(2026, 10, 15, 19, 0, 0), groupDoors: rules);

        Assert.True(morning.IsAllowed);
        Assert.Equal(1, morning.MatchedRuleId);
        Assert.False(evening.IsAllowed);
        Assert.Equal(2, evening.MatchedRuleId);
        Assert.False(afterMidnight.IsAllowed);
        Assert.Equal(2, afterMidnight.MatchedRuleId);
        Assert.False(afternoonGap.IsAllowed);
        Assert.Equal(AccessRuleResolver.OutsideScheduleReason, afternoonGap.Reason);
        Assert.Equal(1, afternoonGap.MatchedRuleId);
    }

    [Fact]
    public void An_empty_higher_level_uses_the_next_matching_level()
    {
        var decision = Resolve(Morning, groupZones: [Rule(4, true, Full(), "CERRAH")]);

        Assert.True(decision.IsAllowed);
        Assert.Equal(MatchedRuleType.AccessGroupZone, decision.MatchedRuleType);
        Assert.Equal("CERRAH yetki grubundan izin verildi.", decision.Reason);
    }

    [Fact]
    public void No_rule_is_default_deny()
    {
        var decision = Resolve(Morning);

        Assert.False(decision.IsAllowed);
        Assert.Equal(AccessRuleResolver.NoPermissionReason, decision.Reason);
        Assert.Equal(MatchedRuleType.None, decision.MatchedRuleType);
    }

    private static AccessDecision Resolve(
        DateTime local,
        IReadOnlyList<AccessRuleSnapshot>? doorOverrides = null,
        IReadOnlyList<AccessRuleSnapshot>? zoneOverrides = null,
        IReadOnlyList<AccessRuleSnapshot>? groupDoors = null,
        IReadOnlyList<AccessRuleSnapshot>? groupZones = null)
    {
        var snapshot = new AccessSnapshot
        {
            PersonnelId = 1,
            DoorId = 10,
            PersonnelExists = true,
            PersonnelIsActive = true,
            PersonnelName = "Ahmet Yılmaz",
            ActiveCards = [new AccessCardSnapshot { Id = 1, CardNumber = "12345678", ValidFrom = new DateOnly(2026, 1, 1) }],
            DoorExists = true,
            DoorIsActive = true,
            ZoneIsActive = true,
            FloorIsActive = true,
            BuildingIsActive = true,
            DoorName = "Server Odası",
            ZoneName = "Bilgi İşlem",
            PersonnelDoorOverrides = doorOverrides ?? [],
            PersonnelZoneOverrides = zoneOverrides ?? [],
            GroupDoorRules = groupDoors ?? [],
            GroupZoneRules = groupZones ?? []
        };
        return AccessRuleResolver.Resolve(snapshot, local, new TimeScheduleEvaluator());
    }

    private static AccessRuleSnapshot Rule(int id, bool allowed, TimeWindow[] windows, string source = "KİŞİSEL") =>
        new()
        {
            RuleId = id,
            IsAllowed = allowed,
            TimeScheduleId = id,
            ScheduleIsActive = true,
            SourceCode = source,
            Windows = windows
        };

    private static TimeWindow[] Full() =>
        Enum.GetValues<DayOfWeek>().Select(day => new TimeWindow(day, new TimeOnly(0, 0), new TimeOnly(0, 0))).ToArray();

    private static TimeWindow[] Office() =>
        new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
            .Select(day => new TimeWindow(day, new TimeOnly(8, 0), new TimeOnly(18, 0)))
            .ToArray();

    private static TimeWindow[] NightHours() =>
        Enum.GetValues<DayOfWeek>().Select(day => new TimeWindow(day, new TimeOnly(22, 0), new TimeOnly(6, 0))).ToArray();
}
