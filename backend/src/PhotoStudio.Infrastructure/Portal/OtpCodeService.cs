using System.Security.Cryptography;
using System.Text;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Portal;
using PhotoStudio.Infrastructure.Identity;

namespace PhotoStudio.Infrastructure.Portal;

/// <summary>
/// Generates verification codes with the system random generator and hashes them with HMAC-SHA256 keyed with the signing
/// secret of the API. A six-digit code has only a million values, so an unkeyed hash could be reversed instantly from a
/// leaked database; with the key, the hash is useless without the secret that lives only in the environment.
/// </summary>
public sealed class OtpCodeService : IOtpCodeService
{
    private readonly byte[] _key;

    /// <summary>
    /// Initializes a new instance of the <see cref="OtpCodeService"/> class.
    /// </summary>
    /// <param name="options">Token settings, whose signing key also keys the code hashes.</param>
    public OtpCodeService(JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _key = Encoding.UTF8.GetBytes(options.SigningKey);
    }

    /// <inheritdoc />
    public string Generate() =>
        RandomNumberGenerator.GetInt32((int)Math.Pow(10, PortalOtp.CodeLength)).ToString($"D{PortalOtp.CodeLength}", System.Globalization.CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public string Hash(Guid bookingId, string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        var payload = Encoding.UTF8.GetBytes($"otp:{bookingId:N}:{code}");
        return Convert.ToBase64String(HMACSHA256.HashData(_key, payload));
    }

    /// <inheritdoc />
    public bool Matches(string expectedHash, string candidateHash)
    {
        ArgumentNullException.ThrowIfNull(expectedHash);
        ArgumentNullException.ThrowIfNull(candidateHash);
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expectedHash), Encoding.UTF8.GetBytes(candidateHash));
    }
}
