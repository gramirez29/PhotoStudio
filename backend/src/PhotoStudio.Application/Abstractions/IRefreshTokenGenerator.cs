namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// A newly generated refresh token: the secret that goes to the app and the hash that is stored.
/// </summary>
/// <param name="Secret">Random secret; shown to the app once and never stored.</param>
/// <param name="Hash">Hash of the secret, the only form that is stored.</param>
public sealed record GeneratedRefreshToken(string Secret, string Hash);

/// <summary>
/// Port that creates and hashes refresh token secrets. Implemented in the Infrastructure layer.
/// </summary>
public interface IRefreshTokenGenerator
{
    /// <summary>
    /// Generates a new random secret and its hash.
    /// </summary>
    /// <returns>The secret and its hash.</returns>
    GeneratedRefreshToken Generate();

    /// <summary>
    /// Hashes a secret presented by the app, to look its token up.
    /// </summary>
    /// <param name="secret">Secret as sent by the app.</param>
    /// <returns>The hash.</returns>
    string Hash(string secret);
}
