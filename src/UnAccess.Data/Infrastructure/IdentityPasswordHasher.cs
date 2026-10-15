using Microsoft.AspNetCore.Identity;
using UnAccess.Core.Abstractions;

namespace UnAccess.Data.Infrastructure;

internal sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<PasswordSubject> _hasher = new();
    private readonly string _dummyHash;

    public IdentityPasswordHasher()
    {
        _dummyHash = _hasher.HashPassword(PasswordSubject.Instance, Guid.NewGuid().ToString("N"));
    }

    public string Hash(string password) => _hasher.HashPassword(PasswordSubject.Instance, password);

    public bool Verify(string hash, string password)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(password))
            return false;

        var result = _hasher.VerifyHashedPassword(PasswordSubject.Instance, hash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }

    public void Burn(string password) => Verify(_dummyHash, string.IsNullOrEmpty(password) ? " " : password);

    private sealed class PasswordSubject
    {
        public static readonly PasswordSubject Instance = new();
    }
}
