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

internal sealed class AccountService : IAccountService
{
    private readonly IDbContextFactory<UnAccessDbContext> _factory;
    private readonly IdentityPasswordHasher _passwords;
    private readonly IUtcClock _clock;
    private readonly ICurrentUser _current;
    private readonly IRuntimeSettings _settings;
    private readonly AuditWriter _audit;

    public AccountService(
        IDbContextFactory<UnAccessDbContext> factory,
        IPasswordHasher passwords,
        IUtcClock clock,
        ICurrentUser current,
        IRuntimeSettings settings,
        AuditWriter audit)
    {
        _factory = factory;
        _passwords = (IdentityPasswordHasher)passwords;
        _clock = clock;
        _current = current;
        _settings = settings;
        _audit = audit;
    }

    public async Task<bool> HasAnyUserAsync(CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.Users.AnyAsync(cancellationToken);
    }

    public async Task<LoginResult> CreateInitialAdminAsync(InitialAdminModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var username = NormalizeUsername(model.Username);
        var fullName = TextRules.Name(model.FullName, "Ad soyad");
        await EnsurePasswordAsync(model.Password, cancellationToken);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await db.Users.AnyAsync(cancellationToken))
            throw new UnAccessException("İlk yönetici zaten oluşturulmuş.");

        var role = await db.Roles.FirstOrDefaultAsync(item => item.Code == RoleCodes.Admin && item.IsActive, cancellationToken);
        if (role == null)
            throw new UnAccessException("Yönetici rolü bulunamadı.");

        var now = _clock.UtcNow;
        var user = new User
        {
            Username = username,
            PasswordHash = _passwords.Hash(model.Password),
            FullName = fullName,
            IsActive = true,
            MustChangePassword = false,
            FailedLoginCount = 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            LastLoginAtUtc = now
        };
        user.UserRoles.Add(new UserRole { Role = role });
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            var result = SignIn(user, [role.Code]);
            AddSecurity(db, user.Id, user.Username, SecurityEventTypes.LoginSuccess, true, "İlk yönetici oluşturuldu", now);
            _audit.Add(db, new AuditEntry
            {
                Action = AuditActions.Created,
                EntityType = AuditEntities.User,
                EntityId = user.Id.ToString(),
                Description = $"İlk yönetici oluşturuldu: {user.Username}"
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            _current.SignOut();
            throw;
        }
    }

    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken)
    {
        var name = (username ?? "").Trim();
        var secret = password ?? "";
        var max = await _settings.LoginMaxFailedAttemptsAsync(cancellationToken);
        var minutes = await _settings.LoginLockoutMinutesAsync(cancellationToken);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users
            .Include(item => item.UserRoles)
            .ThenInclude(item => item.Role)
            .FirstOrDefaultAsync(item => item.Username.ToUpper() == name.ToUpper(), cancellationToken);
        var now = _clock.UtcNow;
        if (user == null || !user.IsActive)
        {
            _passwords.Burn(secret);
            AddSecurity(db, user?.Id, TrimUser(name), SecurityEventTypes.LoginFailed, false, user == null ? "Kullanıcı bulunamadı" : "Pasif kullanıcı", now);
            await db.SaveChangesAsync(cancellationToken);
            return Reject(LoginMessages.InvalidCredentials);
        }

        if (user.LockoutUntilUtc is DateTime until && until > now)
        {
            AddSecurity(db, user.Id, user.Username, SecurityEventTypes.AccountLocked, false, "Kilit süresi dolmadı", now);
            await db.SaveChangesAsync(cancellationToken);
            return Reject(LoginMessages.Locked);
        }

        if (!_passwords.Verify(user.PasswordHash, secret))
        {
            user.FailedLoginCount += 1;
            user.UpdatedAtUtc = now;
            var type = SecurityEventTypes.LoginFailed;
            var detail = "Şifre hatalı";
            var message = LoginMessages.InvalidCredentials;
            if (user.FailedLoginCount >= max)
            {
                user.LockoutUntilUtc = now.AddMinutes(minutes);
                user.FailedLoginCount = 0;
                type = SecurityEventTypes.AccountLocked;
                detail = "Başarısız giriş sınırı aşıldı";
                message = LoginMessages.Locked;
            }

            AddSecurity(db, user.Id, user.Username, type, false, detail, now);
            await db.SaveChangesAsync(cancellationToken);
            return Reject(message);
        }

        user.FailedLoginCount = 0;
        user.LockoutUntilUtc = null;
        user.LastLoginAtUtc = now;
        user.UpdatedAtUtc = now;
        AddSecurity(db, user.Id, user.Username, SecurityEventTypes.LoginSuccess, true, null, now);
        await db.SaveChangesAsync(cancellationToken);
        var roles = user.UserRoles.Where(item => item.Role.IsActive).Select(item => item.Role.Code).ToArray();
        return SignIn(user, roles);
    }

    public async Task ChangeOwnPasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        if (_current.UserId is not int userId)
            throw new UnAccessException("Oturum bulunamadı.");

        await EnsurePasswordAsync(newPassword, cancellationToken);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user == null || !user.IsActive)
            throw new UnAccessException("Kullanıcı bulunamadı.");
        if (!_passwords.Verify(user.PasswordHash, currentPassword ?? ""))
            throw new UnAccessException("Mevcut şifre hatalı.");

        var now = _clock.UtcNow;
        user.PasswordHash = _passwords.Hash(newPassword);
        user.MustChangePassword = false;
        user.UpdatedAtUtc = now;
        AddSecurity(db, user.Id, user.Username, SecurityEventTypes.PasswordChanged, true, null, now);
        _audit.Add(db, new AuditEntry
        {
            Action = AuditActions.Updated,
            EntityType = AuditEntities.User,
            EntityId = user.Id.ToString(),
            Description = "Kullanıcı kendi şifresini değiştirdi."
        });
        await db.SaveChangesAsync(cancellationToken);
        _current.CompletePasswordChange();
    }

    private LoginResult SignIn(User user, IReadOnlyList<string> roles)
    {
        var session = new UserSession
        {
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            MustChangePassword = user.MustChangePassword,
            Roles = roles,
            Permissions = Permissions.ForRoles(roles).ToArray()
        };
        _current.SignIn(session);
        return new LoginResult
        {
            Succeeded = true,
            MustChangePassword = user.MustChangePassword,
            Session = session
        };
    }

    private async Task EnsurePasswordAsync(string? password, CancellationToken cancellationToken)
    {
        var minimum = await _settings.PasswordMinLengthAsync(cancellationToken);
        if (string.IsNullOrEmpty(password) || password.Length < minimum)
            throw new UnAccessException($"Şifre en az {minimum} karakter olmalıdır.");
        if (password.Any(char.IsControl))
            throw new UnAccessException("Şifre geçersiz karakter içeriyor.");
    }

    private static string NormalizeUsername(string? username)
    {
        var text = TextRules.Required(username, "Kullanıcı adı", FieldLengths.Username);
        if (text.Any(char.IsWhiteSpace) || text.Length < 3)
            throw new UnAccessException("Kullanıcı adı en az 3 karakter olmalı ve boşluk içeremez.");
        return text;
    }

    private static string TrimUser(string username) =>
        username.Length <= FieldLengths.Username ? username : username[..FieldLengths.Username];

    private void AddSecurity(UnAccessDbContext db, int? userId, string username, string type, bool success, string? detail, DateTime now)
    {
        db.SecurityEvents.Add(new SecurityEvent
        {
            EventType = type,
            UserId = userId,
            Username = string.IsNullOrWhiteSpace(username) ? "-" : username,
            OccurredAtUtc = now,
            MachineName = Environment.MachineName,
            Detail = detail,
            Success = success
        });
    }

    private static LoginResult Reject(string message) => new() { Succeeded = false, Message = message };
}
