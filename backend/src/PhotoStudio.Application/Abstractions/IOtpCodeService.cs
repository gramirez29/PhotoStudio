namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Port that creates verification codes and hashes them. Implemented in the Infrastructure layer.
/// </summary>
public interface IOtpCodeService
{
    /// <summary>
    /// Generates a random numeric code of <see cref="Domain.Portal.PortalOtp.CodeLength"/> digits.
    /// </summary>
    /// <returns>The code, with leading zeros kept.</returns>
    string Generate();

    /// <summary>
    /// Hashes a code with a secret key and the booking it belongs to. A code has only a million values, so a plain hash would
    /// be easy to reverse from a leaked database; the key and the booking make that useless.
    /// </summary>
    /// <param name="bookingId">Booking the code belongs to.</param>
    /// <param name="code">Code as generated or as typed by the client.</param>
    /// <returns>The hash.</returns>
    string Hash(Guid bookingId, string code);

    /// <summary>
    /// Compares two hashes in constant time.
    /// </summary>
    /// <param name="expectedHash">Stored hash.</param>
    /// <param name="candidateHash">Hash of the typed code.</param>
    /// <returns><see langword="true"/> when they are equal.</returns>
    bool Matches(string expectedHash, string candidateHash);
}
