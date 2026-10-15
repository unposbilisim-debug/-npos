using Microsoft.EntityFrameworkCore;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Audit;
using UnAccess.Core.Common;
using UnAccess.Core.Entities;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Core.Security;
using UnAccess.Data.Infrastructure;

namespace UnAccess.Data.Services;

internal sealed class UserAdminService : IUserAdminService
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly IdentityPasswordHasher _passwords;
    private readonly IUtcClock _clock;
    private readonly ICurrentUser _current;
    private readonly IAccessGuard _guard;
    private readonly IRuntimeSettings _settings;
    private readonly AuditWriter _audit;

    public UserAdminService(
        IDbContextFactory<UnAccessDbContext> factory,
        IPasswordHasher passwords,
        IUtcClock clock,
        ICurrentUser current,
        IAccessGuard guard,
        IRuntimeSettings settings,
        AuditWriter audit)
    {
        _factory = factory;
        _passwords = (IdentityPasswordHasher)passwords;
        _clock = clock;
        _current = current;
        _guard = guard;
        _settings = settings;
        _audit = audit;
    }

    public async Task<IReadOnlyList<UserListItem>> ListAsync(CancellationToken cancellationToken)
    {
        _guard.Demand(Permissions.UsersManage);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Users.AsNoTracking()
            .OrderBy(user => user.Username)
            .Select(user => new UserListItem
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Roles = string.Join(", ", user.UserRoles.Select(link => link.Role.Name).OrderBy(name => name)),
                IsActive = user.IsActive,
                MustChangePassword = user.MustChangePassword,
                LastLoginAtUtc = user.LastLoginAtUtc,
                RoleCodes = user.UserRoles.Select(link => link.Role.Code).ToList()
            })
            .ToListAsync(cancellationToken);
        return rows;
    }

    public async Task<IReadOnlyList<RoleChoice>> ListRolesAsync(CancellationToken cancellationToken)
    {
        _guard.Demand(Permissions.UsersManage);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.Roles.AsNoTracking()
            .Where(role => role.IsActive)
            .OrderBy(role => role.Name)
            .Select(role => new RoleChoice { Code = role.Code, Name = role.Name })
            .ToListAsync(cancellationToken);
    }

    public async Task CreateAsync(UserWriteModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        _guard.Demand(Permissions.UsersManage);
        var username = NormalizeUsername(model.Username);
        var fullName = TextRules.Name(model.FullName, "Ad soyad");
        await EnsurePasswordAsync(model.Password, cancellationToken);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        if (await db.Users.AnyAsync(user => user.Username.ToUpper() == username.ToUpper(), cancellationToken))
            throw new UnAccessException("Bu kullanıcı adı zaten kayıtlı.");

        var roles = await LoadRolesAsync(db, model.RoleCodes, cancellationToken);
        var now = _clock.UtcNow;
        var user = new User
        {
            Username = username,
            PasswordHash = _passwords.Hash(model.Password!),
            FullName = fullName,
            IsActive = model.IsActive,
            MustChangePassword = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        foreach (var role in roles)
            user.UserRoles.Add(new UserRole { Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        AddSecurity(db, user.Id, user.Username, model.IsActive ? SecurityEventTypes.UserActivated : SecurityEventTypes.UserDeactivated, true, "Kullanıcı oluşturuldu", now);
        _audit.Add(db, new AuditEntry
        {
            Action = AuditActions.Created,
            EntityType = AuditEntities.User,
            EntityId = user.Id.ToString(),
            Description = $"Kullanıcı oluşturuldu: {user.Username}",
            NewValue = fullName
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(int id, UserWriteModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        _guard.Demand(Permissions.UsersManage);
        var fullName = TextRules.Name(model.FullName, "Ad soyad");
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.Include(item => item.UserRoles).ThenInclude(item => item.Role)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user == null)
            throw new UnAccessException("Kullanıcı bulunamadı.");

        var roles = await LoadRolesAsync(db, model.RoleCodes, cancellationToken);
        var removingAdmin = user.UserRoles.Any(link => link.Role.Code == RoleCodes.Admin)
            && !roles.Any(role => role.Code == RoleCodes.Admin);
        var deactivating = user.IsActive && !model.IsActive;
        await EnsureAdminSurvivesAsync(db, user, removingAdmin, deactivating && !removingAdmin, cancellationToken);
        if (deactivating && _current.UserId == user.Id)
            throw new UnAccessException("Kendi hesabınızı pasife alamazsınız.");

        var now = _clock.UtcNow;
        var wasActive = user.IsActive;
        user.FullName = fullName;
        user.IsActive = model.IsActive;
        user.UpdatedAtUtc = now;
        var selected = roles.Select(role => role.Id).ToHashSet();
        var removed = user.UserRoles.Where(link => !selected.Contains(link.RoleId)).ToList();
        foreach (var link in removed)
            db.UserRoles.Remove(link);
        foreach (var role in roles.Where(role => user.UserRoles.All(link => link.RoleId != role.Id)))
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });

        if (wasActive != user.IsActive)
            AddSecurity(db, user.Id, user.Username, user.IsActive ? SecurityEventTypes.UserActivated : SecurityEventTypes.UserDeactivated, true, null, now);
        _audit.Add(db, new AuditEntry
        {
            Action = AuditActions.Updated,
            EntityType = AuditEntities.User,
            EntityId = user.Id.ToString(),
            Description = $"Kullanıcı güncellendi: {user.Username}",
            NewValue = fullName
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        _guard.Demand(Permissions.UsersManage);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.Include(item => item.UserRoles).ThenInclude(item => item.Role)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user == null)
            throw new UnAccessException("Kullanıcı bulunamadı.");

        var deactivating = user.IsActive && !isActive;
        await EnsureAdminSurvivesAsync(db, user, removingAdminRole: false, deactivating, cancellationToken);
        if (deactivating && _current.UserId == user.Id)
            throw new UnAccessException("Kendi hesabınızı pasife alamazsınız.");
        if (user.IsActive == isActive)
            return;

        var now = _clock.UtcNow;
        user.IsActive = isActive;
        user.UpdatedAtUtc = now;
        AddSecurity(db, user.Id, user.Username, isActive ? SecurityEventTypes.UserActivated : SecurityEventTypes.UserDeactivated, true, null, now);
        _audit.Add(db, new AuditEntry
        {
            Action = isActive ? AuditActions.Activated : AuditActions.Deactivated,
            EntityType = AuditEntities.User,
            EntityId = user.Id.ToString(),
            Description = isActive ? $"Kullanıcı aktifleştirildi: {user.Username}" : $"Kullanıcı pasife alındı: {user.Username}"
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetPasswordAsync(int id, string temporaryPassword, CancellationToken cancellationToken)
    {
        _guard.Demand(Permissions.UsersManage);
        await EnsurePasswordAsync(temporaryPassword, cancellationToken);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user == null)
            throw new UnAccessException("Kullanıcı bulunamadı.");

        var now = _clock.UtcNow;
        user.PasswordHash = _passwords.Hash(temporaryPassword);
        user.MustChangePassword = true;
        user.FailedLoginCount = 0;
        user.LockoutUntilUtc = null;
        user.UpdatedAtUtc = now;
        AddSecurity(db, user.Id, user.Username, SecurityEventTypes.PasswordReset, true, null, now);
        _audit.Add(db, new AuditEntry
        {
            Action = AuditActions.Updated,
            EntityType = AuditEntities.User,
            EntityId = user.Id.ToString(),
            Description = $"Şifre sıfırlandı: {user.Username}"
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureAdminSurvivesAsync(
        UnAccessDbContext db,
        User user,
        bool removingAdminRole,
        bool deactivating,
        CancellationToken cancellationToken)
    {
        var hadAdmin = user.UserRoles.Any(link => link.Role.Code == RoleCodes.Admin);
        if (!hadAdmin || (!removingAdminRole && !deactivating))
            return;

        var otherActiveAdmins = await db.UserRoles.CountAsync(
            link => link.Role.Code == RoleCodes.Admin && link.User.IsActive && link.UserId != user.Id,
            cancellationToken);
        if (otherActiveAdmins > 0)
            return;

        if (removingAdminRole)
            throw new UnAccessException("Son yönetici rolü kaldırılamaz.");
        throw new UnAccessException("Son aktif yönetici pasife alınamaz.");
    }

    private static async Task<List<Role>> LoadRolesAsync(UnAccessDbContext db, IReadOnlyList<string> codes, CancellationToken cancellationToken)
    {
        var distinct = (codes ?? []).Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => code.Trim()).Distinct(StringComparer.Ordinal).ToArray();
        if (distinct.Length == 0)
            throw new UnAccessException("En az bir rol seçiniz.");

        var roles = await db.Roles.Where(role => role.IsActive && distinct.Contains(role.Code)).ToListAsync(cancellationToken);
        if (roles.Count != distinct.Length)
            throw new UnAccessException("Seçilen rollerden biri bulunamadı.");
        return roles;
    }

    private async Task EnsurePasswordAsync(string? password, CancellationToken cancellationToken)
    {
        var minimum = await _settings.PasswordMinLengthAsync(cancellationToken);
        if (string.IsNullOrEmpty(password) || password.Length < minimum)
            throw new UnAccessException($"Şifre en az {minimum} karakter olmalıdır.");
    }

    private static string NormalizeUsername(string? username)
    {
        var text = TextRules.Required(username, "Kullanıcı adı", FieldLengths.Username);
        if (text.Any(char.IsWhiteSpace) || text.Length < 3)
            throw new UnAccessException("Kullanıcı adı en az 3 karakter olmalı ve boşluk içeremez.");
        return text;
    }

    private static void AddSecurity(UnAccessDbContext db, int userId, string username, string type, bool success, string? detail, DateTime now)
    {
        db.SecurityEvents.Add(new SecurityEvent
        {
            EventType = type,
            UserId = userId,
            Username = username,
            OccurredAtUtc = now,
            MachineName = Environment.MachineName,
            Detail = detail,
            Success = success
        });
    }
}
