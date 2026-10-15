using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Enums;
using UnAccess.Core.Models;
using UnAccess.Core.Security;

namespace UnAccess.Data.Services;

internal sealed class ReportService : IReportService
{
    private const int ExportBatch = 250;
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly IAccessGuard _guard;

    public ReportService(IDbContextFactory<UnAccessDbContext> factory, IAccessGuard guard)
    {
        _factory = factory;
        _guard = guard;
    }

    public async Task<IReadOnlyList<LookupItem>> PersonnelOptionsAsync(CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.ReportsView);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.Personnel.AsNoTracking().OrderBy(item => item.FirstName).ThenBy(item => item.LastName)
            .Select(item => new LookupItem { Id = item.Id, Name = item.FirstName + " " + item.LastName, Code = item.PersonnelNumber, IsActive = item.IsActive })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LookupItem>> DepartmentOptionsAsync(CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.ReportsView);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.Departments.AsNoTracking().OrderBy(item => item.Name)
            .Select(item => new LookupItem { Id = item.Id, Name = item.Name, Code = item.Code, IsActive = item.IsActive })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LookupItem>> DoorOptionsAsync(CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.ReportsView);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.Doors.AsNoTracking().OrderBy(item => item.Name)
            .Select(item => new LookupItem { Id = item.Id, Name = item.Name, Code = item.Code, IsActive = item.IsActive })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LookupItem>> ZoneOptionsAsync(CancellationToken cancellationToken)
    {
        _guard.Require(Permissions.ReportsView);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.Zones.AsNoTracking().OrderBy(item => item.Name)
            .Select(item => new LookupItem { Id = item.Id, Name = item.Name, Code = item.Code, IsActive = item.IsActive })
            .ToListAsync(cancellationToken);
    }

    public Task<ReportPage<PassageReportRow>> PassagesAsync(PassageReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        _guard.Require(Permissions.ReportsView);
        return PageAsync(query, ReportPaging.NormalizePage(query.Page), ReportPaging.NormalizeSize(query.PageSize), cancellationToken);
    }

    public async Task WritePassagesCsvAsync(PassageReportQuery query, Stream output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(output);
        _guard.Require(Permissions.ReportsView);
        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), bufferSize: 1024, leaveOpen: true);
        await writer.WriteLineAsync("Tarih,Personel,Departman,Kart,Kapı,Bölge,Sonuç,Gerekçe");
        DateTime? cursorTime = null;
        var cursorId = 0;
        while (true)
        {
            var batch = await NextPassagesAsync(query, cursorTime, cursorId, cancellationToken);
            if (batch.Count == 0)
                break;

            foreach (var row in batch)
            {
                await writer.WriteLineAsync(string.Join(',',
                    Csv(row.OccurredAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                    Csv(row.PersonnelName),
                    Csv(row.DepartmentName),
                    Csv(row.CardNumber),
                    Csv(row.DoorName),
                    Csv(row.ZoneName),
                    Csv(row.Decision),
                    Csv(row.Reason)));
            }

            var last = batch[^1];
            cursorTime = last.OccurredAtUtc;
            cursorId = last.Id;
            if (batch.Count < ExportBatch)
                break;
        }

        await writer.FlushAsync(cancellationToken);
    }

    public async Task<ReportPage<PersonnelAccessReportRow>> PersonnelAccessAsync(AccessReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        _guard.Require(Permissions.ReportsView);
        var page = ReportPaging.NormalizePage(query.Page);
        var size = ReportPaging.NormalizeSize(query.PageSize);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = PersonnelRows(db, query);
        var total = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderBy(item => item.PersonnelName).ThenBy(item => item.Kind).ThenBy(item => item.Target)
            .Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new ReportPage<PersonnelAccessReportRow> { Items = items, Page = page, PageSize = size, TotalCount = total };
    }

    public async Task WritePersonnelAccessCsvAsync(AccessReportQuery query, Stream output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(output);
        _guard.Require(Permissions.ReportsView);
        await WriteRowsAsync(output, "Sicil,Personel,Departman,Tür,Hedef,Zaman Planı,Etki", await AllPersonnelAsync(query, cancellationToken), row =>
            string.Join(',', Csv(row.PersonnelNumber), Csv(row.PersonnelName), Csv(row.DepartmentName), Csv(row.Kind), Csv(row.Target), Csv(row.Schedule), Csv(row.Effect)), cancellationToken);
    }

    public async Task<ReportPage<DoorAccessReportRow>> DoorAccessAsync(AccessReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        _guard.Require(Permissions.ReportsView);
        var page = ReportPaging.NormalizePage(query.Page);
        var size = ReportPaging.NormalizeSize(query.PageSize);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = DoorRows(db, query);
        var total = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderBy(item => item.DoorName).ThenBy(item => item.Kind).ThenBy(item => item.Subject)
            .Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new ReportPage<DoorAccessReportRow> { Items = items, Page = page, PageSize = size, TotalCount = total };
    }

    public async Task WriteDoorAccessCsvAsync(AccessReportQuery query, Stream output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(output);
        _guard.Require(Permissions.ReportsView);
        await WriteRowsAsync(output, "Kapı,Bölge,Tür,Kime,Zaman Planı,Etki", await AllDoorsAsync(query, cancellationToken), row =>
            string.Join(',', Csv(row.DoorName), Csv(row.ZoneName), Csv(row.Kind), Csv(row.Subject), Csv(row.Schedule), Csv(row.Effect)), cancellationToken);
    }

    private async Task<ReportPage<PassageReportRow>> PageAsync(PassageReportQuery query, int page, int size, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = Passages(db, query);
        var total = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderByDescending(item => item.OccurredAtUtc).ThenByDescending(item => item.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new ReportPage<PassageReportRow> { Items = items, Page = page, PageSize = size, TotalCount = total };
    }

    private async Task<List<PassageReportRow>> NextPassagesAsync(PassageReportQuery query, DateTime? cursorTime, int cursorId, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = Passages(db, query);
        if (cursorTime is DateTime time)
            rows = rows.Where(item => item.OccurredAtUtc < time || (item.OccurredAtUtc == time && item.Id < cursorId));
        return await rows.OrderByDescending(item => item.OccurredAtUtc).ThenByDescending(item => item.Id).Take(ExportBatch).ToListAsync(cancellationToken);
    }

    private async Task<List<PersonnelAccessReportRow>> AllPersonnelAsync(AccessReportQuery query, CancellationToken cancellationToken)
    {
        var page = 1;
        var all = new List<PersonnelAccessReportRow>();
        while (true)
        {
            var batchQuery = new AccessReportQuery
            {
                PersonnelId = query.PersonnelId,
                DepartmentId = query.DepartmentId,
                DoorId = query.DoorId,
                ZoneId = query.ZoneId,
                Page = page,
                PageSize = 250
            };
            var batch = await PersonnelAccessAsync(batchQuery, cancellationToken);
            if (batch.Items.Count == 0)
                break;
            all.AddRange(batch.Items);
            if (batch.Items.Count < 250)
                break;
            page++;
        }

        return all;
    }

    private async Task<List<DoorAccessReportRow>> AllDoorsAsync(AccessReportQuery query, CancellationToken cancellationToken)
    {
        var page = 1;
        var all = new List<DoorAccessReportRow>();
        while (true)
        {
            var batch = await DoorAccessAsync(new AccessReportQuery
            {
                PersonnelId = query.PersonnelId,
                DepartmentId = query.DepartmentId,
                DoorId = query.DoorId,
                ZoneId = query.ZoneId,
                Page = page,
                PageSize = 250
            }, cancellationToken);
            if (batch.Items.Count == 0)
                break;
            all.AddRange(batch.Items);
            if (batch.Items.Count < 250)
                break;
            page++;
        }

        return all;
    }

    private static IQueryable<PassageReportRow> Passages(UnAccessDbContext db, PassageReportQuery query)
    {
        var rows =
            from accessEvent in db.AccessEvents.AsNoTracking()
            join door in db.Doors.AsNoTracking() on accessEvent.DoorId equals door.Id
            join zone in db.Zones.AsNoTracking() on door.ZoneId equals zone.Id
            join person in db.Personnel.AsNoTracking() on accessEvent.PersonnelId equals person.Id into people
            from person in people.DefaultIfEmpty()
            join department in db.Departments.AsNoTracking() on person.DepartmentId equals department.Id into departments
            from department in departments.DefaultIfEmpty()
            select new { accessEvent, door, zone, person, department };

        if (query.DeniedOnly)
            rows = rows.Where(item => item.accessEvent.Decision == nameof(AccessDecisionKind.Denied));
        if (query.FromUtc is DateTime from)
            rows = rows.Where(item => item.accessEvent.OccurredAtUtc >= from);
        if (query.ToUtc is DateTime to)
            rows = rows.Where(item => item.accessEvent.OccurredAtUtc < to);
        if (query.PersonnelId is int personnelId)
            rows = rows.Where(item => item.accessEvent.PersonnelId == personnelId);
        if (query.DepartmentId is int departmentId)
            rows = rows.Where(item => item.person != null && item.person.DepartmentId == departmentId);
        if (query.DoorId is int doorId)
            rows = rows.Where(item => item.accessEvent.DoorId == doorId);
        if (query.ZoneId is int zoneId)
            rows = rows.Where(item => item.door.ZoneId == zoneId);

        return rows.Select(item => new PassageReportRow
        {
            Id = item.accessEvent.Id,
            OccurredAtUtc = item.accessEvent.OccurredAtUtc,
            PersonnelName = item.person == null ? "" : item.person.FirstName + " " + item.person.LastName,
            DepartmentName = item.department == null ? "" : item.department.Name,
            CardNumber = item.accessEvent.CardNumber,
            DoorName = item.door.Name,
            ZoneName = item.zone.Name,
            Decision = item.accessEvent.Decision,
            Reason = item.accessEvent.Reason
        });
    }

    private static IQueryable<PersonnelAccessReportRow> PersonnelRows(UnAccessDbContext db, AccessReportQuery query)
    {
        var memberships = db.PersonnelAccessGroups.AsNoTracking().Where(item => item.IsActive);
        var rules = db.PersonnelAccessOverrides.AsNoTracking().Where(item => item.IsActive);
        if (query.PersonnelId is int personnelId)
        {
            memberships = memberships.Where(item => item.PersonnelId == personnelId);
            rules = rules.Where(item => item.PersonnelId == personnelId);
        }

        if (query.DepartmentId is int departmentId)
        {
            memberships = memberships.Where(item => db.Personnel.Any(person => person.Id == item.PersonnelId && person.DepartmentId == departmentId));
            rules = rules.Where(item => db.Personnel.Any(person => person.Id == item.PersonnelId && person.DepartmentId == departmentId));
        }

        var groups =
            from membership in memberships
            join person in db.Personnel.AsNoTracking() on membership.PersonnelId equals person.Id
            join department in db.Departments.AsNoTracking() on person.DepartmentId equals department.Id
            join accessGroup in db.AccessGroups.AsNoTracking() on membership.AccessGroupId equals accessGroup.Id
            select new PersonnelAccessReportRow
            {
                PersonnelNumber = person.PersonnelNumber,
                PersonnelName = person.FirstName + " " + person.LastName,
                DepartmentName = department.Name,
                Kind = "Grup",
                Target = accessGroup.Name,
                Schedule = "",
                Effect = "Üyelik"
            };
        var overrides =
            from rule in rules
            join person in db.Personnel.AsNoTracking() on rule.PersonnelId equals person.Id
            join department in db.Departments.AsNoTracking() on person.DepartmentId equals department.Id
            join schedule in db.TimeSchedules.AsNoTracking() on rule.TimeScheduleId equals schedule.Id
            join zone in db.Zones.AsNoTracking() on rule.ZoneId equals zone.Id into zones
            from zone in zones.DefaultIfEmpty()
            join door in db.Doors.AsNoTracking() on rule.DoorId equals door.Id into doors
            from door in doors.DefaultIfEmpty()
            select new PersonnelAccessReportRow
            {
                PersonnelNumber = person.PersonnelNumber,
                PersonnelName = person.FirstName + " " + person.LastName,
                DepartmentName = department.Name,
                Kind = "İstisna",
                Target = door != null ? door.Name : zone != null ? zone.Name : "",
                Schedule = schedule.Name,
                Effect = rule.IsAllowed ? "İzin" : "Yasak"
            };
        return groups.Concat(overrides);
    }

    private static IQueryable<DoorAccessReportRow> DoorRows(UnAccessDbContext db, AccessReportQuery query)
    {
        var doorRules = db.AccessGroupDoors.AsNoTracking().Where(item => item.IsActive);
        var zoneRules = db.AccessGroupZones.AsNoTracking().Where(item => item.IsActive);
        var peopleRules = db.PersonnelAccessOverrides.AsNoTracking().Where(item => item.IsActive);
        if (query.DoorId is int doorId)
        {
            doorRules = doorRules.Where(item => item.DoorId == doorId);
            zoneRules = zoneRules.Where(item => db.Doors.Any(door => door.Id == doorId && door.ZoneId == item.ZoneId));
            peopleRules = peopleRules.Where(item => item.DoorId == doorId || db.Doors.Any(door => door.Id == doorId && door.ZoneId == item.ZoneId));
        }

        if (query.ZoneId is int zoneId)
        {
            doorRules = doorRules.Where(item => db.Doors.Any(door => door.Id == item.DoorId && door.ZoneId == zoneId));
            zoneRules = zoneRules.Where(item => item.ZoneId == zoneId);
            peopleRules = peopleRules.Where(item => item.ZoneId == zoneId || db.Doors.Any(door => door.Id == item.DoorId && door.ZoneId == zoneId));
        }

        var groupDoors =
            from rule in doorRules
            join door in db.Doors.AsNoTracking() on rule.DoorId equals door.Id
            join zone in db.Zones.AsNoTracking() on door.ZoneId equals zone.Id
            join accessGroup in db.AccessGroups.AsNoTracking() on rule.AccessGroupId equals accessGroup.Id
            join schedule in db.TimeSchedules.AsNoTracking() on rule.TimeScheduleId equals schedule.Id
            select new DoorAccessReportRow
            {
                DoorName = door.Name,
                ZoneName = zone.Name,
                Kind = "Grup kapı",
                Subject = accessGroup.Name,
                Schedule = schedule.Name,
                Effect = rule.IsAllowed ? "İzin" : "Yasak"
            };
        var groupZones =
            from rule in zoneRules
            join zone in db.Zones.AsNoTracking() on rule.ZoneId equals zone.Id
            join door in db.Doors.AsNoTracking() on zone.Id equals door.ZoneId
            join accessGroup in db.AccessGroups.AsNoTracking() on rule.AccessGroupId equals accessGroup.Id
            join schedule in db.TimeSchedules.AsNoTracking() on rule.TimeScheduleId equals schedule.Id
            where door.IsActive
            select new DoorAccessReportRow
            {
                DoorName = door.Name,
                ZoneName = zone.Name,
                Kind = "Grup bölge",
                Subject = accessGroup.Name,
                Schedule = schedule.Name,
                Effect = rule.IsAllowed ? "İzin" : "Yasak"
            };
        var people =
            from rule in peopleRules
            join person in db.Personnel.AsNoTracking() on rule.PersonnelId equals person.Id
            join schedule in db.TimeSchedules.AsNoTracking() on rule.TimeScheduleId equals schedule.Id
            join door in db.Doors.AsNoTracking() on rule.DoorId equals door.Id into doors
            from door in doors.DefaultIfEmpty()
            join doorZone in db.Zones.AsNoTracking() on door.ZoneId equals doorZone.Id into doorZones
            from doorZone in doorZones.DefaultIfEmpty()
            join zone in db.Zones.AsNoTracking() on rule.ZoneId equals zone.Id into zones
            from zone in zones.DefaultIfEmpty()
            where door != null || zone != null
            select new DoorAccessReportRow
            {
                DoorName = door != null ? door.Name : "",
                ZoneName = doorZone != null ? doorZone.Name : zone!.Name,
                Kind = "Personel istisnası",
                Subject = person.FirstName + " " + person.LastName,
                Schedule = schedule.Name,
                Effect = rule.IsAllowed ? "İzin" : "Yasak"
            };
        return groupDoors.Concat(groupZones).Concat(people);
    }

    private static async Task WriteRowsAsync<T>(Stream output, string header, IReadOnlyList<T> rows, Func<T, string> line, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), bufferSize: 1024, leaveOpen: true);
        await writer.WriteLineAsync(header);
        foreach (var row in rows)
            await writer.WriteLineAsync(line(row));
        await writer.FlushAsync(cancellationToken);
    }

    private static string Csv(string? value)
    {
        var text = value ?? "";
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@')
            text = "'" + text;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
            return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        return text;
    }
}
