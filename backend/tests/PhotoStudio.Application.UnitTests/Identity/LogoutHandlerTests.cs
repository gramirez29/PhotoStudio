using PhotoStudio.Application.Identity.Logout;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.UnitTests.Identity;

/// <summary>
/// Tests of <see cref="LogoutHandler"/>.
/// </summary>
public sealed class LogoutHandlerTests
{
    private readonly IdentityFixture _fixture = new();

    /// <summary>
    /// Closing a known session revokes its whole family.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAKnownToken_RevokesTheFamily()
    {
        var token = IdentityFixture.StoredToken(IdentityFixture.Account());
        _fixture.RefreshTokens.GetByHashAsync("hash-of-secret", Arg.Any<CancellationToken>()).Returns(token);

        var response = await NewHandler().HandleAsync(new LogoutCommand("secret"), TestContext.Current.CancellationToken);

        response.SessionClosed.ShouldBeTrue();
        await _fixture.RefreshTokens.Received(1).RevokeFamilyAsync(token.FamilyId, IdentityFixture.Now, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An unknown token is not an error: the call is idempotent and reports that nothing was closed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAnUnknownToken_DoesNothing()
    {
        _fixture.RefreshTokens.GetByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((RefreshToken?)null);

        var response = await NewHandler().HandleAsync(new LogoutCommand("secret"), TestContext.Current.CancellationToken);

        response.SessionClosed.ShouldBeFalse();
        await _fixture.RefreshTokens.DidNotReceive().RevokeFamilyAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A blank token is ignored without touching the database.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithABlankToken_DoesNothing()
    {
        var response = await NewHandler().HandleAsync(new LogoutCommand("  "), TestContext.Current.CancellationToken);

        response.SessionClosed.ShouldBeFalse();
        await _fixture.RefreshTokens.DidNotReceive().GetByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Creates the handler under test.
    /// </summary>
    /// <returns>The handler.</returns>
    private LogoutHandler NewHandler() => new(_fixture.RefreshTokens, _fixture.TokenGenerator, _fixture.Clock);
}
