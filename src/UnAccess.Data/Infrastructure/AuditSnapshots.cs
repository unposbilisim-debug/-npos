using UnAccess.Core.Entities;

namespace UnAccess.Data.Infrastructure;

internal static class AuditSnapshots
{
    public static string Personnel(Personnel person) =>
        $"Sicil={person.PersonnelNumber}; Ad={person.FirstName}; Soyad={person.LastName}; DepartmanId={person.DepartmentId}; GorevId={person.JobTitleId}; Telefon={person.Phone}; Eposta={person.Email}; Foto={person.PhotoPath}; Aktif={person.IsActive}";

    public static string Card(Card card) =>
        $"KartNo={card.CardNumber}; PersonelId={card.PersonnelId}; Durum={card.Status}; Baslangic={card.ValidFrom:yyyy-MM-dd}; Bitis={card.ValidUntil:yyyy-MM-dd}; Aciklama={card.Description}";

    public static string Building(Building building) =>
        $"Kod={building.Code}; Ad={building.Name}; Aktif={building.IsActive}";

    public static string Floor(Floor floor) =>
        $"BinaId={floor.BuildingId}; Kod={floor.Code}; Ad={floor.Name}; KatNo={floor.FloorNumber}; Aktif={floor.IsActive}";

    public static string Zone(Zone zone) =>
        $"KatId={zone.FloorId}; Kod={zone.Code}; Ad={zone.Name}; Kritik={zone.IsCritical}; Aktif={zone.IsActive}";

    public static string AccessGroup(AccessGroup group) =>
        $"Kod={group.Code}; Ad={group.Name}; Aciklama={group.Description}; Aktif={group.IsActive}";

    public static string PersonnelAccessGroup(PersonnelAccessGroup membership) =>
        $"PersonelId={membership.PersonnelId}; GrupId={membership.AccessGroupId}; Baslangic={membership.ValidFrom:yyyy-MM-dd}; Bitis={membership.ValidUntil:yyyy-MM-dd}; Aktif={membership.IsActive}";

    public static string AccessGroupZone(AccessGroupZone rule) =>
        $"GrupId={rule.AccessGroupId}; BolgeId={rule.ZoneId}; PlanId={rule.TimeScheduleId}; Izin={rule.IsAllowed}; Aktif={rule.IsActive}";

    public static string AccessGroupDoor(AccessGroupDoor rule) =>
        $"GrupId={rule.AccessGroupId}; KapiId={rule.DoorId}; PlanId={rule.TimeScheduleId}; Izin={rule.IsAllowed}; Aktif={rule.IsActive}";

    public static string PersonnelAccessOverride(PersonnelAccessOverride rule) =>
        $"PersonelId={rule.PersonnelId}; BolgeId={rule.ZoneId}; KapiId={rule.DoorId}; PlanId={rule.TimeScheduleId}; Izin={rule.IsAllowed}; Baslangic={rule.ValidFrom:yyyy-MM-dd}; Bitis={rule.ValidUntil:yyyy-MM-dd}; Gerekce={rule.Reason}; Aktif={rule.IsActive}";

    public static string TimeSchedule(TimeSchedule schedule, IEnumerable<string> intervals) =>
        $"Kod={schedule.Code}; Ad={schedule.Name}; Aciklama={schedule.Description}; Aktif={schedule.IsActive}; Aralıklar={string.Join("|", intervals)}";

    public static string Door(Door door) =>
        $"BolgeId={door.ZoneId}; Kod={door.Code}; Ad={door.Name}; KontrolorId={door.ControllerId}; Kanal={door.ChannelNumber}; Giris={door.EntryReaderName}; Cikis={door.ExitReaderName}; CikisOkuyucu={door.HasExitReader}; Kritik={door.IsCritical}; Aktif={door.IsActive}";
}
