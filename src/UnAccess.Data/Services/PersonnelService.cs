using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Audit;
using UnAccess.Core.Common;
using UnAccess.Core.Entities;
using UnAccess.Core.Enums;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Data.Services;

internal sealed class PersonnelService : ServiceBase, IPersonnelService
{
    private readonly IDeviceSyncOutbox _outbox;

    public PersonnelService(
        IDbContextFactory<UnAccessDbContext> factory,
        AuditWriter audit,
        ILogger<PersonnelService> logger,
        IDeviceSyncOutbox outbox)
        : base(factory, audit, logger)
    {
        _outbox = outbox;
    }

    public Task<IReadOnlyList<PersonnelListItem>> SearchAsync(PersonnelSearchCriteria criteria, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return QueryAsync(async (db, ct) =>
        {
            var query =
                from person in db.Personnel.AsNoTracking()
                join department in db.Departments.AsNoTracking() on person.DepartmentId equals department.Id
                join jobTitle in db.JobTitles.AsNoTracking() on person.JobTitleId equals jobTitle.Id
                select new { person, department, jobTitle };

            if (criteria.DepartmentId is int departmentId)
                query = query.Where(x => x.person.DepartmentId == departmentId);
            if (criteria.IsActive is bool isActive)
                query = query.Where(x => x.person.IsActive == isActive);
            if (!string.IsNullOrWhiteSpace(criteria.Text))
            {
                var term = LikePattern.Contains(criteria.Text);
                query = query.Where(x =>
                    EF.Functions.Like(x.person.FirstName, term) ||
                    EF.Functions.Like(x.person.LastName, term) ||
                    EF.Functions.Like(x.person.PersonnelNumber, term) ||
                    EF.Functions.Like(x.person.FirstName + " " + x.person.LastName, term));
            }

            var rows = await query
                .OrderBy(x => x.person.PersonnelNumber)
                .Select(x => new PersonnelListItem
                {
                    Id = x.person.Id,
                    PersonnelNumber = x.person.PersonnelNumber,
                    FirstName = x.person.FirstName,
                    LastName = x.person.LastName,
                    DepartmentName = x.department.Name,
                    JobTitleName = x.jobTitle.Name,
                    IsActive = x.person.IsActive,
                    ActiveCardNumber = db.Cards
                        .Where(card => card.PersonnelId == x.person.Id && card.Status == CardStatus.Active)
                        .OrderByDescending(card => card.Id)
                        .Select(card => card.CardNumber)
                        .FirstOrDefault() ?? ""
                })
                .ToListAsync(ct);

            return (IReadOnlyList<PersonnelListItem>)rows;
        }, cancellationToken);
    }

    public Task<PersonnelDetail?> GetAsync(int id, CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var row = await (
                from person in db.Personnel.AsNoTracking()
                join department in db.Departments.AsNoTracking() on person.DepartmentId equals department.Id
                join jobTitle in db.JobTitles.AsNoTracking() on person.JobTitleId equals jobTitle.Id
                where person.Id == id
                select new { person, department, jobTitle }).FirstOrDefaultAsync(ct);

            if (row == null)
                return null;

            var cards = await db.Cards.AsNoTracking()
                .Where(card => card.PersonnelId == id)
                .OrderByDescending(card => card.Id)
                .Select(card => new PersonnelCardItem
                {
                    Id = card.Id,
                    CardNumber = card.CardNumber,
                    Status = card.Status,
                    ValidFrom = card.ValidFrom,
                    ValidUntil = card.ValidUntil,
                    Description = card.Description
                })
                .ToListAsync(ct);

            return MapDetail(row.person, row.department, row.jobTitle, cards);
        }, cancellationToken);
    }

    public async Task<PersonnelDetail> CreateAsync(PersonnelEditModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        Department? department = null;
        JobTitle? jobTitle = null;

        var person = await SaveWithAuditAsync(async (db, ct) =>
        {
            var draft = Build(model);
            department = await RequireDepartmentAsync(db, draft.DepartmentId, currentDepartmentId: null, ct);
            jobTitle = await RequireJobTitleAsync(db, draft.JobTitleId, currentJobTitleId: null, ct);
            await EnsureNumberAvailableAsync(db, draft.PersonnelNumber, excludeId: null, ct);
            db.Personnel.Add(draft);
            return draft;
        }, saved => new AuditEntry
        {
            Action = AuditActions.Created,
            EntityType = AuditEntities.Personnel,
            EntityId = saved.Id.ToString(),
            Description = $"Personel oluşturuldu: {saved.PersonnelNumber} {saved.FirstName} {saved.LastName}",
            NewValue = AuditSnapshots.Personnel(saved)
        }, cancellationToken);

        return MapDetail(person, department!, jobTitle!, Array.Empty<PersonnelCardItem>());
    }

    public async Task<PersonnelDetail> UpdateAsync(int id, PersonnelEditModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        Department? department = null;
        JobTitle? jobTitle = null;
        var oldSnapshot = "";
        var action = AuditActions.Updated;
        var cardAudits = new List<AuditEntry>();

        var person = await SaveWithAuditAsync(async (db, ct) =>
        {
            var existing = await db.Personnel.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (existing == null)
                throw new UnAccessException("Personel bulunamadı.");

            var draft = Build(model);
            department = await RequireDepartmentAsync(db, draft.DepartmentId, existing.DepartmentId, ct);
            jobTitle = await RequireJobTitleAsync(db, draft.JobTitleId, existing.JobTitleId, ct);
            await EnsureNumberAvailableAsync(db, draft.PersonnelNumber, existing.Id, ct);

            oldSnapshot = AuditSnapshots.Personnel(existing);
            action = existing.IsActive == draft.IsActive
                ? AuditActions.Updated
                : draft.IsActive ? AuditActions.Activated : AuditActions.Deactivated;

            if (existing.IsActive && !draft.IsActive)
                cardAudits.AddRange(await CancelActiveCardsAsync(db, existing.Id, ct));

            existing.PersonnelNumber = draft.PersonnelNumber;
            existing.FirstName = draft.FirstName;
            existing.LastName = draft.LastName;
            existing.DepartmentId = draft.DepartmentId;
            existing.JobTitleId = draft.JobTitleId;
            existing.Phone = draft.Phone;
            existing.Email = draft.Email;
            existing.PhotoPath = draft.PhotoPath;
            existing.IsActive = draft.IsActive;
            return existing;
        }, saved =>
        {
            var audits = new List<AuditEntry>
            {
                new()
                {
                    Action = action,
                    EntityType = AuditEntities.Personnel,
                    EntityId = saved.Id.ToString(),
                    Description = action switch
                    {
                        AuditActions.Deactivated => $"Personel pasife alındı: {saved.PersonnelNumber} {saved.FirstName} {saved.LastName}",
                        AuditActions.Activated => $"Personel aktifleştirildi: {saved.PersonnelNumber} {saved.FirstName} {saved.LastName}",
                        _ => $"Personel güncellendi: {saved.PersonnelNumber} {saved.FirstName} {saved.LastName}"
                    },
                    OldValue = oldSnapshot,
                    NewValue = AuditSnapshots.Personnel(saved)
                }
            };
            audits.AddRange(cardAudits);
            return audits;
        }, cancellationToken);

        var cards = await GetCardsAsync(person.Id, cancellationToken);
        return MapDetail(person, department!, jobTitle!, cards);
    }

    public Task<IReadOnlyList<LookupItem>> GetDepartmentsAsync(CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var rows = await db.Departments.AsNoTracking()
                .OrderBy(d => d.Name)
                .Select(d => new LookupItem { Id = d.Id, Code = d.Code, Name = d.Name, IsActive = d.IsActive })
                .ToListAsync(ct);
            return (IReadOnlyList<LookupItem>)rows;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<LookupItem>> GetJobTitlesAsync(CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var rows = await db.JobTitles.AsNoTracking()
                .OrderBy(j => j.Name)
                .Select(j => new LookupItem { Id = j.Id, Code = j.Code, Name = j.Name, IsActive = j.IsActive })
                .ToListAsync(ct);
            return (IReadOnlyList<LookupItem>)rows;
        }, cancellationToken);
    }

    private async Task<IReadOnlyList<PersonnelCardItem>> GetCardsAsync(int personnelId, CancellationToken cancellationToken)
    {
        return await QueryAsync(async (db, ct) =>
        {
            var rows = await db.Cards.AsNoTracking()
                .Where(card => card.PersonnelId == personnelId)
                .OrderByDescending(card => card.Id)
                .Select(card => new PersonnelCardItem
                {
                    Id = card.Id,
                    CardNumber = card.CardNumber,
                    Status = card.Status,
                    ValidFrom = card.ValidFrom,
                    ValidUntil = card.ValidUntil,
                    Description = card.Description
                })
                .ToListAsync(ct);
            return (IReadOnlyList<PersonnelCardItem>)rows;
        }, cancellationToken);
    }

    private async Task<IReadOnlyList<AuditEntry>> CancelActiveCardsAsync(
        UnAccessDbContext db,
        int personnelId,
        CancellationToken cancellationToken)
    {
        var activeCards = await db.Cards
            .Where(card => card.PersonnelId == personnelId && card.Status == CardStatus.Active)
            .ToListAsync(cancellationToken);

        var audits = new List<AuditEntry>(activeCards.Count);
        foreach (var card in activeCards)
        {
            var oldValue = AuditSnapshots.Card(card);
            card.Status = CardStatus.Cancelled;
            await _outbox.EnqueueCardAsync(db, card, remove: true, cancellationToken);
            audits.Add(new AuditEntry
            {
                Action = AuditActions.StatusChanged,
                EntityType = AuditEntities.Card,
                EntityId = card.Id.ToString(),
                Description = $"Personel pasife alındığı için kart iptal edildi: {card.CardNumber}",
                OldValue = oldValue,
                NewValue = AuditSnapshots.Card(card)
            });
        }

        return audits;
    }

    private static Personnel Build(PersonnelEditModel model) => new()
    {
        PersonnelNumber = TextRules.PersonnelNumber(model.PersonnelNumber),
        FirstName = TextRules.PersonName(model.FirstName, "Ad"),
        LastName = TextRules.PersonName(model.LastName, "Soyad"),
        DepartmentId = model.DepartmentId,
        JobTitleId = model.JobTitleId,
        Phone = TextRules.Phone(model.Phone),
        Email = TextRules.Email(model.Email),
        PhotoPath = TextRules.Optional(model.PhotoPath, "Fotoğraf yolu", FieldLengths.PhotoPath),
        IsActive = model.IsActive
    };

    private static async Task<Department> RequireDepartmentAsync(
        UnAccessDbContext db,
        int departmentId,
        int? currentDepartmentId,
        CancellationToken cancellationToken)
    {
        if (departmentId <= 0)
            throw new UnAccessException("Departman seçiniz.");

        var department = await db.Departments.FirstOrDefaultAsync(d => d.Id == departmentId, cancellationToken);
        if (department == null)
            throw new UnAccessException("Departman bulunamadı.");
        if (!department.IsActive && department.Id != currentDepartmentId)
            throw new UnAccessException("Pasif departman seçilemez.");

        return department;
    }

    private static async Task<JobTitle> RequireJobTitleAsync(
        UnAccessDbContext db,
        int jobTitleId,
        int? currentJobTitleId,
        CancellationToken cancellationToken)
    {
        if (jobTitleId <= 0)
            throw new UnAccessException("Görev seçiniz.");

        var jobTitle = await db.JobTitles.FirstOrDefaultAsync(j => j.Id == jobTitleId, cancellationToken);
        if (jobTitle == null)
            throw new UnAccessException("Görev bulunamadı.");
        if (!jobTitle.IsActive && jobTitle.Id != currentJobTitleId)
            throw new UnAccessException("Pasif görev seçilemez.");

        return jobTitle;
    }

    private static async Task EnsureNumberAvailableAsync(
        UnAccessDbContext db,
        string personnelNumber,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        var taken = await db.Personnel.AnyAsync(
            p => p.PersonnelNumber == personnelNumber && (excludeId == null || p.Id != excludeId),
            cancellationToken);
        if (taken)
            throw new UnAccessException("Bu sicil numarası zaten kayıtlı.");
    }

    private static PersonnelDetail MapDetail(
        Personnel person,
        Department department,
        JobTitle jobTitle,
        IReadOnlyList<PersonnelCardItem> cards) => new()
    {
        Id = person.Id,
        PersonnelNumber = person.PersonnelNumber,
        FirstName = person.FirstName,
        LastName = person.LastName,
        DepartmentId = department.Id,
        DepartmentName = department.Name,
        JobTitleId = jobTitle.Id,
        JobTitleName = jobTitle.Name,
        Phone = person.Phone,
        Email = person.Email,
        PhotoPath = person.PhotoPath,
        IsActive = person.IsActive,
        Cards = cards
    };
}
