using Microsoft.AspNetCore.Identity;
using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Infrastructure.Identity;

/// <summary>
/// Password hashing with the ASP.NET Core Identity hasher: PBKDF2 with a random salt and a work factor that the framework
/// raises over time, in a self-describing format so old hashes keep verifying after an upgrade.
/// </summary>
public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private static readonly Credential Subject = new();

    private readonly PasswordHasher<Credential> _hasher = new();
    private readonly Lazy<string> _decoyHash;

    /// <summary>
    /// Initializes a new instance of the <see cref="AspNetPasswordHasher"/> class.
    /// </summary>
    public AspNetPasswordHasher()
    {
        _decoyHash = new Lazy<string>(() => _hasher.HashPassword(Subject, Guid.NewGuid().ToString("N")));
    }

    /// <inheritdoc />
    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return _hasher.HashPassword(Subject, password);
    }

    /// <inheritdoc />
    public bool Verify(string passwordHash, string password)
    {
        ArgumentNullException.ThrowIfNull(passwordHash);
        ArgumentNullException.ThrowIfNull(password);

        // A hash that needs a rehash still verifies; it is upgraded the next time the password is set.
        return _hasher.VerifyHashedPassword(Subject, passwordHash, password) != PasswordVerificationResult.Failed;
    }

    /// <inheritdoc />
    public void SpendVerificationTime(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        _ = _hasher.VerifyHashedPassword(Subject, _decoyHash.Value, password);
    }

    /// <summary>
    /// Placeholder subject for the generic hasher, which does not use the user object.
    /// </summary>
    private sealed class Credential;
}
