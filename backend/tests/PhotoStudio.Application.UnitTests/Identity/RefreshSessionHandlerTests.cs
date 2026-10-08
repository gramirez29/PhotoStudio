using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Identity.RefreshSession;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.UnitTests.Identity;

/// <summary>
/// Tests of <see cref="RefreshSessionHandler"/>.
/// </summary>
public sealed class RefreshSessionHandlerTests
{
    private const string PresentedSecret = "presented-secret";

    private readonly IdentityFixture _fixture = new();

    /// <summary>
    /// A valid token is rotated: the old one is swapped for a new one of the same family and a new session is returned.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAValidToken_RotatesItAndReturnsANewSession()
    {
        var account = IdentityFixture.NewUser();
        var current = GivenStoredToken(account);
        _fixture.RefreshTokens.RotateAsync(current, Arg.Any<RefreshToken>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(true);

        var session = await NewHandler().HandleAsync(new RefreshSessionCommand(PresentedSecret), TestContext.Current.CancellationToken);

        session.PhotographerId.ShouldBe(account.Id);
        session.AccessToken.ShouldBe("access-jwt");
        session.RefreshToken.ShouldBe("new-secret");
        await _fixture.RefreshTokens.Received(1).RotateAsync(
            current,
            Arg.Is<RefreshToken>(replacement => replacement.FamilyId == current.FamilyId && replacement.TokenHash == "new-hash"),
            IdentityFixture.Now,
            Arg.Any<CancellationToken>());
        await _fixture.RefreshTokens.DidNotReceive().RevokeFamilyAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A token nobody issued is rejected generically.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAnUnknownToken_IsRejected()
    {
        _fixture.RefreshTokens.GetByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((RefreshToken?)null);

        var exception = await Should.ThrowAsync<AuthenticationFailedException>(
            () => NewHandler().HandleAsync(new RefreshSessionCommand(PresentedSecret), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidRefreshToken);
        await _fixture.RefreshTokens.DidNotReceive().RotateAsync(Arg.Any<RefreshToken>(), Arg.Any<RefreshToken>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An empty token is rejected without touching the database.
    /// </summary>
    /// <param name="secret">Empty or blank secret.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_WithABlankToken_IsRejectedWithoutALookup(string secret)
    {
        await Should.ThrowAsync<AuthenticationFailedException>(
            () => NewHandler().HandleAsync(new RefreshSessionCommand(secret), TestContext.Current.CancellationToken));

        await _fixture.RefreshTokens.DidNotReceive().GetByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A token that was already used is a replay: it is rejected and its whole family is revoked.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAnAlreadyUsedToken_RevokesTheWholeFamily()
    {
        var account = IdentityFixture.NewUser();
        var used = GivenStoredToken(account, revoked: true);

        var exception = await Should.ThrowAsync<AuthenticationFailedException>(
            () => NewHandler().HandleAsync(new RefreshSessionCommand(PresentedSecret), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidRefreshToken);
        await _fixture.RefreshTokens.Received(1).RevokeFamilyAsync(used.FamilyId, IdentityFixture.Now, Arg.Any<CancellationToken>());
        await _fixture.RefreshTokens.DidNotReceive().RotateAsync(Arg.Any<RefreshToken>(), Arg.Any<RefreshToken>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An expired token is rejected.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAnExpiredToken_IsRejected()
    {
        var account = IdentityFixture.NewUser();
        GivenStoredToken(account, expiresAt: IdentityFixture.Now);

        var exception = await Should.ThrowAsync<AuthenticationFailedException>(
            () => NewHandler().HandleAsync(new RefreshSessionCommand(PresentedSecret), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidRefreshToken);
        await _fixture.RefreshTokens.DidNotReceive().RotateAsync(Arg.Any<RefreshToken>(), Arg.Any<RefreshToken>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A token whose user no longer exists is rejected and its family revoked.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenTheUserNoLongerExists_RevokesTheFamily()
    {
        var account = IdentityFixture.NewUser();
        var token = GivenStoredToken(account);
        _fixture.Users.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns((User?)null);

        await Should.ThrowAsync<AuthenticationFailedException>(
            () => NewHandler().HandleAsync(new RefreshSessionCommand(PresentedSecret), TestContext.Current.CancellationToken));

        await _fixture.RefreshTokens.Received(1).RevokeFamilyAsync(token.FamilyId, IdentityFixture.Now, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When another request rotated the same token first, this one is rejected and the family is revoked.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenAnotherRequestRotatedTheTokenFirst_RevokesTheFamily()
    {
        var account = IdentityFixture.NewUser();
        var current = GivenStoredToken(account);
        _fixture.RefreshTokens.RotateAsync(current, Arg.Any<RefreshToken>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(false);

        var exception = await Should.ThrowAsync<AuthenticationFailedException>(
            () => NewHandler().HandleAsync(new RefreshSessionCommand(PresentedSecret), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidRefreshToken);
        await _fixture.RefreshTokens.Received(1).RevokeFamilyAsync(current.FamilyId, IdentityFixture.Now, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Makes the repositories return a stored token (found by the hash of the presented secret) and its account.
    /// </summary>
    /// <param name="account">Owner of the token.</param>
    /// <param name="revoked">Whether the token was already used.</param>
    /// <param name="expiresAt">Expiry of the token.</param>
    /// <returns>The stored token.</returns>
    private RefreshToken GivenStoredToken(User account, bool revoked = false, DateTimeOffset? expiresAt = null)
    {
        var token = IdentityFixture.StoredToken(account, revoked, expiresAt);
        _fixture.RefreshTokens.GetByHashAsync("hash-of-" + PresentedSecret, Arg.Any<CancellationToken>()).Returns(token);
        _fixture.Users.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        return token;
    }

    /// <summary>
    /// Creates the handler under test.
    /// </summary>
    /// <returns>The handler.</returns>
    private RefreshSessionHandler NewHandler() => new(
        _fixture.Users,
        _fixture.RefreshTokens,
        _fixture.TokenGenerator,
        _fixture.Sessions,
        _fixture.Clock);
}
