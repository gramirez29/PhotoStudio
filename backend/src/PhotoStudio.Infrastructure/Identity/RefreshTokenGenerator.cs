using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Infrastructure.Identity;

/// <summary>
/// Generates refresh token secrets (256 bits from the system random generator) and hashes them with SHA-256. A fast hash is
/// enough here because the secret already has full entropy: there is nothing to brute-force, unlike a password.
/// </summary>
public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    private const int SecretBytes = 32;

    /// <inheritdoc />
    public GeneratedRefreshToken Generate()
    {
        var secret = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretBytes));
        return new GeneratedRefreshToken(secret, Hash(secret));
    }

    /// <inheritdoc />
    public string Hash(string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    }
}
