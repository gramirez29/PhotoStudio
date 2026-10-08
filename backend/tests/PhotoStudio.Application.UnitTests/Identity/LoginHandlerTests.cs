using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Identity.Login;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.UnitTests.Identity;

/// <summary>
/// Tests of <see cref="LoginHandler"/>.
/// </summary>
public sealed class LoginHandlerTests
{
    private readonly IdentityFixture _fixture = new();

    /// <summary>
    /// Correct credentials start a session and store the hash of its refresh token, never the secret.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithCorrectCredentials_StartsASession()
    {
        var account = IdentityFixture.Account();
        GivenAccount(account, passwordMatches: true);

        var session = await NewHandler().HandleAsync(new LoginCommand("ana@example.com", "correct horse"), TestContext.Current.CancellationToken);

        session.PhotographerId.ShouldBe(account.Id);
        session.Email.ShouldBe("ana@example.com");
        session.AccessToken.ShouldBe("access-jwt");
        session.RefreshToken.ShouldBe("new-secret");
        session.RefreshTokenExpiresAt.ShouldBe(IdentityFixture.Now + IdentityFixture.RefreshLifetime);
        await _fixture.RefreshTokens.Received(1).AddAsync(
            Arg.Is<RefreshToken>(token => token.PhotographerId == account.Id && token.TokenHash == "new-hash"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The email is trimmed and lower-cased before the lookup.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_NormalizesTheEmailBeforeTheLookup()
    {
        GivenAccount(IdentityFixture.Account(), passwordMatches: true);

        await NewHandler().HandleAsync(new LoginCommand("  ANA@Example.COM ", "correct horse"), TestContext.Current.CancellationToken);

        await _fixture.Accounts.Received(1).GetByEmailAsync("ana@example.com", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Each login starts its own family, so signing out of one device does not sign out the others.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_StartsANewFamilyOnEveryLogin()
    {
        GivenAccount(IdentityFixture.Account(), passwordMatches: true);
        var families = new List<Guid>();
        await _fixture.RefreshTokens.AddAsync(Arg.Do<RefreshToken>(token => families.Add(token.FamilyId)), Arg.Any<CancellationToken>());

        await NewHandler().HandleAsync(new LoginCommand("ana@example.com", "correct horse"), TestContext.Current.CancellationToken);
        await NewHandler().HandleAsync(new LoginCommand("ana@example.com", "correct horse"), TestContext.Current.CancellationToken);

        families.Count.ShouldBe(2);
        families[0].ShouldNotBe(families[1]);
    }

    /// <summary>
    /// A success after earlier failures clears the failure counter.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_AfterEarlierFailures_ResetsTheCounter()
    {
        var account = IdentityFixture.Account(failedLoginAttempts: 3);
        GivenAccount(account, passwordMatches: true);

        await NewHandler().HandleAsync(new LoginCommand("ana@example.com", "correct horse"), TestContext.Current.CancellationToken);

        await _fixture.Accounts.Received(1).ResetFailedLoginsAsync(account.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A clean success does not write to the account.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithACleanAccount_DoesNotTouchTheCounter()
    {
        GivenAccount(IdentityFixture.Account(), passwordMatches: true);

        await NewHandler().HandleAsync(new LoginCommand("ana@example.com", "correct horse"), TestContext.Current.CancellationToken);

        await _fixture.Accounts.DidNotReceive().ResetFailedLoginsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An unknown email fails like a wrong password and still spends the time of a verification.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithUnknownEmail_FailsGenericallyAfterHashingWork()
    {
        _fixture.Accounts.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((PhotographerAccount?)null);

        var exception = await Should.ThrowAsync<AuthenticationFailedException>(
            () => NewHandler().HandleAsync(new LoginCommand("nobody@example.com", "whatever"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidCredentials);
        _fixture.PasswordHasher.Received(1).SpendVerificationTime("whatever");
        _fixture.PasswordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        await _fixture.RefreshTokens.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An email that is not an email never reaches the database and fails the same way.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithInvalidEmail_FailsGenericallyWithoutALookup()
    {
        var exception = await Should.ThrowAsync<AuthenticationFailedException>(
            () => NewHandler().HandleAsync(new LoginCommand("not an email", "whatever"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidCredentials);
        await _fixture.Accounts.DidNotReceive().GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _fixture.PasswordHasher.Received(1).SpendVerificationTime("whatever");
    }

    /// <summary>
    /// A wrong password fails generically and counts one failed attempt, without locking below the limit.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithWrongPassword_CountsTheFailure()
    {
        var account = IdentityFixture.Account();
        GivenAccount(account, passwordMatches: false);
        _fixture.Accounts.RegisterFailedLoginAsync(account.Id, Arg.Any<CancellationToken>()).Returns(PhotographerAccount.MaxFailedLoginAttempts - 1);

        var exception = await Should.ThrowAsync<AuthenticationFailedException>(
            () => NewHandler().HandleAsync(new LoginCommand("ana@example.com", "wrong"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidCredentials);
        await _fixture.Accounts.Received(1).RegisterFailedLoginAsync(account.Id, Arg.Any<CancellationToken>());
        await _fixture.Accounts.DidNotReceive().LockAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _fixture.RefreshTokens.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The failure that reaches the limit locks the account for the lockout duration.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenTheLimitIsReached_LocksTheAccount()
    {
        var account = IdentityFixture.Account(failedLoginAttempts: PhotographerAccount.MaxFailedLoginAttempts - 1);
        GivenAccount(account, passwordMatches: false);
        _fixture.Accounts.RegisterFailedLoginAsync(account.Id, Arg.Any<CancellationToken>()).Returns(PhotographerAccount.MaxFailedLoginAttempts);

        await Should.ThrowAsync<AuthenticationFailedException>(
            () => NewHandler().HandleAsync(new LoginCommand("ana@example.com", "wrong"), TestContext.Current.CancellationToken));

        await _fixture.Accounts.Received(1).LockAsync(
            account.Id,
            IdentityFixture.Now + PhotographerAccount.LockoutDuration,
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A locked account refuses even the correct password and says how long to wait.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithALockedAccount_RefusesWithoutVerifyingThePassword()
    {
        var account = IdentityFixture.Account(lockedUntil: IdentityFixture.Now.AddMinutes(10));
        GivenAccount(account, passwordMatches: true);

        var exception = await Should.ThrowAsync<AccountLockedException>(
            () => NewHandler().HandleAsync(new LoginCommand("ana@example.com", "correct horse"), TestContext.Current.CancellationToken));

        exception.RetryAfter.ShouldBe(TimeSpan.FromMinutes(10));
        exception.Code.ShouldBe(ApplicationErrorCodes.AccountLocked);
        _fixture.PasswordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        await _fixture.RefreshTokens.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Once the lock has passed, the account accepts the correct password again.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_AfterTheLockEnds_AcceptsTheCorrectPassword()
    {
        var account = IdentityFixture.Account(lockedUntil: IdentityFixture.Now.AddSeconds(-1));
        GivenAccount(account, passwordMatches: true);

        var session = await NewHandler().HandleAsync(new LoginCommand("ana@example.com", "correct horse"), TestContext.Current.CancellationToken);

        session.PhotographerId.ShouldBe(account.Id);
        await _fixture.Accounts.Received(1).ResetFailedLoginsAsync(account.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A missing password is treated as a wrong one, not as a crash.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithANullPassword_FailsGenerically()
    {
        var account = IdentityFixture.Account();
        GivenAccount(account, passwordMatches: false);

        await Should.ThrowAsync<AuthenticationFailedException>(
            () => NewHandler().HandleAsync(new LoginCommand("ana@example.com", null!), TestContext.Current.CancellationToken));

        _fixture.PasswordHasher.Received(1).Verify(account.PasswordHash, string.Empty);
    }

    /// <summary>
    /// Makes the repository return the account and the hasher accept or reject the password.
    /// </summary>
    /// <param name="account">Stored account.</param>
    /// <param name="passwordMatches">Whether the password verifies.</param>
    private void GivenAccount(PhotographerAccount account, bool passwordMatches)
    {
        _fixture.Accounts.GetByEmailAsync(account.Email, Arg.Any<CancellationToken>()).Returns(account);
        _fixture.PasswordHasher.Verify(account.PasswordHash, Arg.Any<string>()).Returns(passwordMatches);
    }

    /// <summary>
    /// Creates the handler under test.
    /// </summary>
    /// <returns>The handler.</returns>
    private LoginHandler NewHandler() => new(
        _fixture.Accounts,
        _fixture.RefreshTokens,
        _fixture.PasswordHasher,
        _fixture.Sessions,
        _fixture.Clock);
}
