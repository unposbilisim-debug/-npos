using UnAccess.Core.Enums;
using UnAccess.Core.Models;

namespace UnAccess.Core.Access;

public sealed class AccessSnapshot
{
    public int PersonnelId { get; init; }
    public int DoorId { get; init; }
    public bool PersonnelExists { get; init; }
    public bool PersonnelIsActive { get; init; }
    public string PersonnelName { get; init; } = "";
    public IReadOnlyList<AccessCardSnapshot> ActiveCards { get; init; } = [];
    public bool DoorExists { get; init; }
    public bool DoorIsActive { get; init; }
    public bool ZoneIsActive { get; init; }
    public bool FloorIsActive { get; init; }
    public bool BuildingIsActive { get; init; }
    public string DoorName { get; init; } = "";
    public string ZoneName { get; init; } = "";
    public string Location { get; init; } = "";
    public int? ControllerId { get; init; }
    public int ChannelNumber { get; init; }
    public IReadOnlyList<AccessRuleSnapshot> PersonnelDoorOverrides { get; init; } = [];
    public IReadOnlyList<AccessRuleSnapshot> PersonnelZoneOverrides { get; init; } = [];
    public IReadOnlyList<AccessRuleSnapshot> GroupDoorRules { get; init; } = [];
    public IReadOnlyList<AccessRuleSnapshot> GroupZoneRules { get; init; } = [];
}

public sealed class AccessCardSnapshot
{
    public int Id { get; init; }
    public string CardNumber { get; init; } = "";
    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidUntil { get; init; }
}

public sealed class AccessRuleSnapshot
{
    public int RuleId { get; init; }
    public bool IsAllowed { get; init; }
    public int TimeScheduleId { get; init; }
    public bool ScheduleIsActive { get; init; }
    public string SourceCode { get; init; } = "";
    public IReadOnlyList<TimeWindow> Windows { get; init; } = [];
}

public static class AccessRuleResolver
{
    public const string NoPermissionReason = "Bu kapı için geçerli yetki bulunamadı.";
    public const string OutsideScheduleReason = "Zaman planı dışında.";

    public static AccessDecision Resolve(
        AccessSnapshot snapshot,
        DateTime hospitalLocal,
        ITimeScheduleEvaluator schedules)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(schedules);

        var denied = Base(snapshot);
        if (!snapshot.PersonnelExists)
            return denied("Personel bulunamadı.");
        if (!snapshot.PersonnelIsActive)
            return denied("Personel pasif.");
        if (snapshot.ActiveCards.Count == 0)
            return denied("Aktif kartı yok.");

        var localDate = DateOnly.FromDateTime(hospitalLocal);
        var card = snapshot.ActiveCards
            .Where(item => item.ValidFrom <= localDate && (item.ValidUntil == null || item.ValidUntil >= localDate))
            .OrderBy(item => item.Id)
            .FirstOrDefault();
        if (card == null)
            return denied("Kartın geçerlilik tarihi uygun değil.");
        if (!snapshot.DoorExists)
            return denied("Kapı bulunamadı.");
        if (!snapshot.DoorIsActive)
            return denied("Kapı pasif.");
        if (!snapshot.ZoneIsActive)
            return denied("Bölge pasif.");
        if (!snapshot.FloorIsActive || !snapshot.BuildingIsActive)
            return denied("Bina veya kat pasif.");

        foreach (var level in Levels(snapshot))
        {
            var candidates = level.Rules.OrderBy(rule => rule.RuleId).ToList();
            if (candidates.Count == 0)
                continue;

            // This level already has an active rule for the door. A schedule miss stays here.
            var matching = candidates
                .Where(rule => schedules.IsMatch(rule.ScheduleIsActive, rule.Windows, hospitalLocal))
                .ToList();
            var blocked = matching.FirstOrDefault(rule => !rule.IsAllowed);
            if (blocked != null)
                return FromRule(snapshot, card, blocked, level.Type, allowed: false);
            var allowed = matching.FirstOrDefault(rule => rule.IsAllowed);
            if (allowed != null)
                return FromRule(snapshot, card, allowed, level.Type, allowed: true);

            var outside = candidates[0];
            return Finish(
                snapshot,
                card,
                false,
                OutsideScheduleReason,
                level.Type,
                outside.RuleId,
                outside.TimeScheduleId,
                SourceName(level.Type, outside));
        }

        return Finish(snapshot, card, false, NoPermissionReason, MatchedRuleType.None, null, null, "");
    }

    private static IEnumerable<(MatchedRuleType Type, IReadOnlyList<AccessRuleSnapshot> Rules)> Levels(AccessSnapshot snapshot)
    {
        yield return (MatchedRuleType.PersonnelDoorOverride, snapshot.PersonnelDoorOverrides);
        yield return (MatchedRuleType.PersonnelZoneOverride, snapshot.PersonnelZoneOverrides);
        yield return (MatchedRuleType.AccessGroupDoor, snapshot.GroupDoorRules);
        yield return (MatchedRuleType.AccessGroupZone, snapshot.GroupZoneRules);
    }

    private static AccessDecision FromRule(
        AccessSnapshot snapshot,
        AccessCardSnapshot card,
        AccessRuleSnapshot rule,
        MatchedRuleType type,
        bool allowed)
    {
        var personal = type is MatchedRuleType.PersonnelDoorOverride or MatchedRuleType.PersonnelZoneOverride;
        var reason = allowed
            ? personal ? "Kişisel izin nedeniyle geçiş onaylandı." : $"{rule.SourceCode} yetki grubundan izin verildi."
            : personal ? "Kişisel yasak nedeniyle geçiş reddedildi." : $"{rule.SourceCode} yetki grubundan yasaklandı.";
        return Finish(snapshot, card, allowed, reason, type, rule.RuleId, rule.TimeScheduleId, SourceName(type, rule));
    }

    private static string SourceName(MatchedRuleType type, AccessRuleSnapshot rule)
    {
        var personal = type is MatchedRuleType.PersonnelDoorOverride or MatchedRuleType.PersonnelZoneOverride;
        return personal ? "KİŞİSEL İSTİSNA" : rule.SourceCode;
    }

    private static Func<string, AccessDecision> Base(AccessSnapshot snapshot) =>
        reason => new AccessDecision
        {
            IsAllowed = false,
            Reason = reason,
            PersonnelId = snapshot.PersonnelId,
            DoorId = snapshot.DoorId,
            MatchedRuleType = MatchedRuleType.None,
            PersonnelName = snapshot.PersonnelName,
            DoorName = snapshot.DoorName,
            ZoneName = snapshot.ZoneName,
            Location = snapshot.Location,
            ControllerId = snapshot.ControllerId,
            ChannelNumber = snapshot.ChannelNumber,
            CardNumber = snapshot.ActiveCards.OrderBy(card => card.Id).FirstOrDefault()?.CardNumber ?? ""
        };

    private static AccessDecision Finish(
        AccessSnapshot snapshot,
        AccessCardSnapshot card,
        bool allowed,
        string reason,
        MatchedRuleType type,
        int? ruleId,
        int? scheduleId,
        string source)
    {
        return new AccessDecision
        {
            IsAllowed = allowed,
            Reason = reason,
            PersonnelId = snapshot.PersonnelId,
            DoorId = snapshot.DoorId,
            MatchedRuleType = type,
            MatchedRuleId = ruleId,
            TimeScheduleId = scheduleId,
            CardId = card.Id,
            CardNumber = card.CardNumber,
            PersonnelName = snapshot.PersonnelName,
            DoorName = snapshot.DoorName,
            ZoneName = snapshot.ZoneName,
            Location = snapshot.Location,
            ControllerId = snapshot.ControllerId,
            ChannelNumber = snapshot.ChannelNumber,
            SourceName = source
        };
    }
}
