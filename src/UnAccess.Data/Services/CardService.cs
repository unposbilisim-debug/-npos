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

internal sealed class CardService : ServiceBase, ICardService
{
    public CardService(IDbContextFactory<UnAccessDbContext> factory, AuditWriter audit, ILogger<CardService> logger)
        : base(factory, audit, logger)
    {
    }

    public Task<IReadOnlyList<CardListItem>> SearchAsync(CardSearchCriteria criteria, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return QueryAsync(async (db, ct) =>
        {
            var query = Cards(db);
            if (!string.IsNullOrWhiteSpace(criteria.Text))
            {
                var term = LikePattern.Contains(criteria.Text);
                query = query.Where(x =>
                    EF.Functions.Like(x.CardNumber, term) ||
                    EF.Functions.Like(x.PersonnelName, term) ||
                    EF.Functions.Like(x.PersonnelNumber, term));
            }

            var rows = await query.OrderBy(x => x.CardNumber).ToListAsync(ct);
            return (IReadOnlyList<CardListItem>)rows;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<CardListItem>> GetByPersonnelAsync(int personnelId, CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var rows = await Cards(db)
                .Where(x => x.PersonnelId == personnelId)
                .OrderByDescending(x => x.Id)
                .ToListAsync(ct);
            return (IReadOnlyList<CardListItem>)rows;
        }, cancellationToken);
    }

    public async Task<CardListItem> CreateAsync(CardEditModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        string personnelName = "";
        string personnelNumber = "";
        string departmentName = "";

        var card = await SaveWithAuditAsync(async (db, ct) =>
        {
            var cardNumber = TextRules.CardNumber(model.CardNumber);
            var description = TextRules.Optional(model.Description, "Açıklama", FieldLengths.Description);
            EnsureDates(model.ValidFrom, model.ValidUntil);
            if (model.PersonnelId <= 0)
                throw new UnAccessException("Personel seçiniz.");

            var person = await (
                from p in db.Personnel
                join d in db.Departments on p.DepartmentId equals d.Id
                where p.Id == model.PersonnelId
                select new { p, d }).FirstOrDefaultAsync(ct);
            if (person == null)
                throw new UnAccessException("Personel bulunamadı.");
            if (!person.p.IsActive)
                throw new UnAccessException("Pasif personele kart verilemez.");

            var taken = await db.Cards.AnyAsync(c => c.CardNumber == cardNumber, ct);
            if (taken)
                throw new UnAccessException("Bu kart numarası zaten kayıtlı.");

            personnelName = $"{person.p.FirstName} {person.p.LastName}";
            personnelNumber = person.p.PersonnelNumber;
            departmentName = person.d.Name;

            var entity = new Card
            {
                PersonnelId = person.p.Id,
                CardNumber = cardNumber,
                Status = CardStatus.Active,
                ValidFrom = model.ValidFrom,
                ValidUntil = model.ValidUntil,
                Description = description
            };
            db.Cards.Add(entity);
            return entity;
        }, saved => new AuditEntry
        {
            Action = AuditActions.Created,
            EntityType = AuditEntities.Card,
            EntityId = saved.Id.ToString(),
            Description = $"Kart oluşturuldu: {saved.CardNumber}",
            NewValue = AuditSnapshots.Card(saved)
        }, cancellationToken);

        return new CardListItem
        {
            Id = card.Id,
            CardNumber = card.CardNumber,
            PersonnelId = card.PersonnelId,
            PersonnelName = personnelName,
            PersonnelNumber = personnelNumber,
            DepartmentName = departmentName,
            Status = card.Status,
            ValidFrom = card.ValidFrom,
            ValidUntil = card.ValidUntil
        };
    }

    public async Task<CardListItem> SetStatusAsync(int id, CardStatus status, CancellationToken cancellationToken)
    {
        if (status is not (CardStatus.Active or CardStatus.Lost or CardStatus.Cancelled))
            throw new UnAccessException("Kart yalnızca aktif, kayıp veya iptal yapılabilir.");

        var oldSnapshot = "";
        string personnelName = "";
        string personnelNumber = "";
        string departmentName = "";

        var card = await SaveWithAuditAsync(async (db, ct) =>
        {
            var existing = await db.Cards.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (existing == null)
                throw new UnAccessException("Kart bulunamadı.");
            if (existing.Status == status)
                throw new UnAccessException($"Kart zaten {status.ToTurkish()} durumunda.");

            var person = await (
                from p in db.Personnel.AsNoTracking()
                join d in db.Departments.AsNoTracking() on p.DepartmentId equals d.Id
                where p.Id == existing.PersonnelId
                select new { p, d }).FirstAsync(ct);

            personnelName = $"{person.p.FirstName} {person.p.LastName}";
            personnelNumber = person.p.PersonnelNumber;
            departmentName = person.d.Name;
            oldSnapshot = AuditSnapshots.Card(existing);
            existing.Status = status;
            return existing;
        }, saved => new AuditEntry
        {
            Action = AuditActions.StatusChanged,
            EntityType = AuditEntities.Card,
            EntityId = saved.Id.ToString(),
            Description = $"Kart durumu değişti: {saved.CardNumber} -> {saved.Status.ToTurkish()}",
            OldValue = oldSnapshot,
            NewValue = AuditSnapshots.Card(saved)
        }, cancellationToken);

        return new CardListItem
        {
            Id = card.Id,
            CardNumber = card.CardNumber,
            PersonnelId = card.PersonnelId,
            PersonnelName = personnelName,
            PersonnelNumber = personnelNumber,
            DepartmentName = departmentName,
            Status = card.Status,
            ValidFrom = card.ValidFrom,
            ValidUntil = card.ValidUntil
        };
    }

    public Task<IReadOnlyList<PersonnelOption>> GetPersonnelOptionsAsync(CancellationToken cancellationToken)
    {
        return QueryAsync(async (db, ct) =>
        {
            var rows = await db.Personnel.AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.PersonnelNumber)
                .Select(p => new PersonnelOption
                {
                    Id = p.Id,
                    PersonnelNumber = p.PersonnelNumber,
                    FullName = p.FirstName + " " + p.LastName
                })
                .ToListAsync(ct);
            return (IReadOnlyList<PersonnelOption>)rows;
        }, cancellationToken);
    }

    private static IQueryable<CardListItem> Cards(UnAccessDbContext db) =>
        from card in db.Cards.AsNoTracking()
        join person in db.Personnel.AsNoTracking() on card.PersonnelId equals person.Id
        join department in db.Departments.AsNoTracking() on person.DepartmentId equals department.Id
        select new CardListItem
        {
            Id = card.Id,
            CardNumber = card.CardNumber,
            PersonnelId = person.Id,
            PersonnelName = person.FirstName + " " + person.LastName,
            PersonnelNumber = person.PersonnelNumber,
            DepartmentName = department.Name,
            Status = card.Status,
            ValidFrom = card.ValidFrom,
            ValidUntil = card.ValidUntil
        };

    private static void EnsureDates(DateOnly validFrom, DateOnly? validUntil)
    {
        if (validFrom == default)
            throw new UnAccessException("Başlangıç tarihi zorunludur.");
        if (validUntil is DateOnly until && until < validFrom)
            throw new UnAccessException("Bitiş tarihi başlangıçtan önce olamaz.");
    }

}
