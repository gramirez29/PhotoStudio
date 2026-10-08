using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Identity;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.UnitTests.Identity;

/// <summary>
/// Shared setup of the authentication handler tests: substituted ports, a fixed clock and a real <see cref="SessionIssuer"/>
/// fed by predictable tokens, so the tests can assert exactly what was issued and stored.
/// </summary>
internal sealed class IdentityFixture
{
    /// <summary>
    /// Instant returned by the clock of every test.
    /// </summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Lifetime of the refresh tokens in the tests.
    /// </summary>
    public static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityFixture"/> class and configures the default behavior of the ports.
    /// </summary>
    public IdentityFixture()
    {
        AccessTokens.Issue(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
            .Returns(call => new AccessToken("access-jwt", call.Arg<DateTimeOffset>().AddMinutes(15)));
        TokenGenerator.Generate().Returns(new GeneratedRefreshToken("new-secret", "new-hash"));
        TokenGenerator.Hash(Arg.Any<string>()).Returns(call => "hash-of-" + call.Arg<string>());
        Sessions = new SessionIssuer(AccessTokens, TokenGenerator, new AuthSessionSettings(RefreshLifetime));
    }

    /// <summary>Gets the substituted account repository.</summary>
    public IPhotographerAccountRepository Accounts { get; } = Substitute.For<IPhotographerAccountRepository>();

    /// <summary>Gets the substituted refresh token repository.</summary>
    public IRefreshTokenRepository RefreshTokens { get; } = Substitute.For<IRefreshTokenRepository>();

    /// <summary>Gets the substituted password hasher.</summary>
    public IPasswordHasher PasswordHasher { get; } = Substitute.For<IPasswordHasher>();

    /// <summary>Gets the substituted access token issuer.</summary>
    public IAccessTokenIssuer AccessTokens { get; } = Substitute.For<IAccessTokenIssuer>();

    /// <summary>Gets the substituted refresh token generator.</summary>
    public IRefreshTokenGenerator TokenGenerator { get; } = Substitute.For<IRefreshTokenGenerator>();

    /// <summary>Gets the real session issuer built on the substituted ports.</summary>
    public SessionIssuer Sessions { get; }

    /// <summary>Gets the clock, fixed at <see cref="Now"/>.</summary>
    public TimeProvider Clock { get; } = new FixedTimeProvider(Now);

    /// <summary>
    /// Builds a stored account.
    /// </summary>
    /// <param name="failedLoginAttempts">Consecutive failed logins already counted.</param>
    /// <param name="lockedUntil">Lock end, if the account is locked.</param>
    /// <returns>The account.</returns>
    public static PhotographerAccount Account(int failedLoginAttempts = 0, DateTimeOffset? lockedUntil = null) =>
        PhotographerAccount.Restore(Guid.CreateVersion7(), 1, "ana@example.com", "stored-hash", Now.AddDays(-30), failedLoginAttempts, lockedUntil);

    /// <summary>
    /// Builds a stored refresh token for an account.
    /// </summary>
    /// <param name="account">Owner of the token.</param>
    /// <param name="revoked">Whether the token was already used or revoked.</param>
    /// <param name="expiresAt">Expiry; defaults to 10 days from <see cref="Now"/>.</param>
    /// <returns>The token.</returns>
    public static RefreshToken StoredToken(PhotographerAccount account, bool revoked = false, DateTimeOffset? expiresAt = null) =>
        RefreshToken.Restore(
            Guid.CreateVersion7(),
            account.Id,
            Guid.CreateVersion7(),
            "hash-of-presented-secret",
            Now.AddDays(-20),
            expiresAt ?? Now.AddDays(10),
            revoked ? Now.AddDays(-1) : null,
            revoked ? Guid.CreateVersion7() : null);
}
