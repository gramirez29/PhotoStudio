using System.Globalization;

namespace PhotoStudio.Infrastructure.Identity;

/// <summary>
/// Settings of the access tokens and sessions, read from environment variables (Railway service variables in production).
/// The signing key is a secret and must never be committed.
/// </summary>
/// <param name="SigningKey">Secret used to sign and validate the access tokens (HMAC-SHA256).</param>
/// <param name="Issuer">Value of the <c>iss</c> claim.</param>
/// <param name="Audience">Value of the <c>aud</c> claim.</param>
/// <param name="AccessTokenLifetime">How long an access token is accepted.</param>
/// <param name="RefreshTokenLifetime">How long a refresh token is valid.</param>
public sealed record JwtOptions(
    string SigningKey,
    string Issuer,
    string Audience,
    TimeSpan AccessTokenLifetime,
    TimeSpan RefreshTokenLifetime)
{
    /// <summary>Environment variable that holds the signing key.</summary>
    public const string SigningKeyVariable = "JWT_SIGNING_KEY";

    /// <summary>Environment variable that holds the issuer.</summary>
    public const string IssuerVariable = "JWT_ISSUER";

    /// <summary>Environment variable that holds the audience.</summary>
    public const string AudienceVariable = "JWT_AUDIENCE";

    /// <summary>Environment variable that holds the access token lifetime, in minutes.</summary>
    public const string AccessTokenMinutesVariable = "JWT_ACCESS_TOKEN_MINUTES";

    /// <summary>Environment variable that holds the refresh token lifetime, in days.</summary>
    public const string RefreshTokenDaysVariable = "JWT_REFRESH_TOKEN_DAYS";

    /// <summary>Default issuer.</summary>
    public const string DefaultIssuer = "photostudio-api";

    /// <summary>Default audience.</summary>
    public const string DefaultAudience = "photostudio-app";

    /// <summary>Default access token lifetime, in minutes.</summary>
    public const int DefaultAccessTokenMinutes = 15;

    /// <summary>Default refresh token lifetime, in days.</summary>
    public const int DefaultRefreshTokenDays = 30;

    /// <summary>Minimum length of the signing key, in characters.</summary>
    public const int MinimumSigningKeyLength = 32;

    /// <summary>
    /// Marker contained in the key committed for local development. A key with it is refused outside Development, so the
    /// public development key can never sign tokens in production.
    /// </summary>
    public const string DevelopmentKeyMarker = "dev-only";

    /// <summary>
    /// Reads and validates the options from the process environment.
    /// </summary>
    /// <param name="isDevelopment">Whether the API runs in the Development environment.</param>
    /// <returns>The options.</returns>
    /// <exception cref="InvalidOperationException">When the signing key is missing, too short or is the development key outside Development, or a lifetime is not a valid number in range.</exception>
    public static JwtOptions FromEnvironment(bool isDevelopment)
    {
        var signingKey = Environment.GetEnvironmentVariable(SigningKeyVariable);
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException($"The environment variable '{SigningKeyVariable}' is not set.");
        }

        if (signingKey.Length < MinimumSigningKeyLength)
        {
            throw new InvalidOperationException($"'{SigningKeyVariable}' must have at least {MinimumSigningKeyLength} characters.");
        }

        if (!isDevelopment && signingKey.Contains(DevelopmentKeyMarker, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"'{SigningKeyVariable}' is the development key; set a secret generated for this environment.");
        }

        return new JwtOptions(
            signingKey,
            ReadText(IssuerVariable, DefaultIssuer),
            ReadText(AudienceVariable, DefaultAudience),
            TimeSpan.FromMinutes(ReadNumber(AccessTokenMinutesVariable, DefaultAccessTokenMinutes, 1, 60)),
            TimeSpan.FromDays(ReadNumber(RefreshTokenDaysVariable, DefaultRefreshTokenDays, 1, 365)));
    }

    /// <summary>
    /// Hides the signing key from logs and debugger output.
    /// </summary>
    /// <returns>A string without secrets.</returns>
    public override string ToString() => $"JwtOptions {{ Issuer = {Issuer}, Audience = {Audience} }}";

    /// <summary>
    /// Reads a text variable.
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <param name="fallback">Value used when the variable is empty or missing.</param>
    /// <returns>The trimmed value or the fallback.</returns>
    private static string ReadText(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    /// <summary>
    /// Reads an integer variable within a range.
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <param name="fallback">Value used when the variable is empty or missing.</param>
    /// <param name="minimum">Smallest accepted value.</param>
    /// <param name="maximum">Largest accepted value.</param>
    /// <returns>The value or the fallback.</returns>
    /// <exception cref="InvalidOperationException">When the value is not an integer in range.</exception>
    private static int ReadNumber(string name, int fallback, int minimum, int maximum)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < minimum || number > maximum)
        {
            throw new InvalidOperationException($"'{name}' must be a whole number between {minimum} and {maximum}.");
        }

        return number;
    }
}
