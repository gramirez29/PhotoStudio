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
        var account = IdentityFixture.NewUser();
        GivenAccount(account, passwordMatches: true);

        var session = await NewHandler().HandleAsync(new LoginCommand("ana", "correct horse"), TestContext.Current.CancellationToken);

        session.PhotographerId.ShouldBe(account.Id);
        session.Username.ShouldBe("ana");
        session.AccessToken.ShouldBe("access-jwt");
        session.RefreshToken.ShouldBe("new-secret");
        session.RefreshTokenExpiresAt.ShouldBe(IdentityFixture.Now + IdentityFixture.RefreshLifetime);
        await _fixture.RefreshTokens.Received(1).AddAsync(
            Arg.Is<RefreshToken>(token => token.PhotographerId == account.Id && token.TokenHash == "new-hash"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The username is trimmed and lower-cased before the lookup.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_NormalizesTheUsernameBeforeTheLookup()
    {
        GivenAccount(IdentityFixture.NewUser(), passwordMatches: true);

        await NewHandler().HandleAsync(new LoginCommand("  ANA ", "correct horse"), TestContext.Current.CancellationToken);

        await _fixture.Users.Received(1).GetByUsernameAsync("ana", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Each login starts its own family, so signing out of one device does not sign out the others.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_StartsANewFamilyOnEveryLogin()
    {
        GivenAccount(IdentityFixture.NewUser(), passwordMatches: true);
        var families = new List<Guid>();
        await _fixture.RefreshTokens.AddAsync(Arg.Do<RefreshToken>(token => families.Add(token.FamilyId)), Arg.Any<CancellationToken>());

        await NewHandler().HandleAsync(new LoginCommand("ana", "correct horse"), TestContext.Current.CancellationToken);
        await NewHandler().HandleAsync(new LoginCommand("ana", "correct horse"), TestContext.Current.CancellationToken);

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
        var account = IdentityFixture.NewUser(failedLoginAttempts: 3);
        GivenAccount(account, passwordMatches: true);

        await NewHandler().HandleAsync(new LoginCommand("ana", "correct horse"), TestContext.Current.CancellationToken);

        await _fixture.Users.Received(1).ResetFailedLoginsAsync(account.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A clean success does not write to the user.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithACleanAccount_DoesNotTouchTheCounter()
    {
        GivenAccount(IdentityFixture.NewUser(), passwordMatches: true);

        await NewHandler().HandleAsync(new LoginCommand("ana", "correct horse"), TestContext.Current.CancellationToken);

        await _fixture.Users.DidNotReceive().ResetFailedLoginsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An unknown username fails like a wrong password and still spends the time of a verification.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithUnknownUsername_FailsGenericallyAfterHashingWork()
    {
        _fixture.Users.GetByUsernameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var exception = await Should.ThrowAsync<AuthenticationFailedException>(
            () => NewHandler().HandleAsync(new LoginCommand("nobody", "whatever"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidCredentials);
        _fixture.PasswordHasher.Received(1).SpendVerificationTime("whatever");
        _fixture.PasswordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        await _fixture.RefreshTokens.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A username with no valid shape never reaches the database and fails the same way.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithInvalidUsername_FailsGenericallyWithoutALookup()
    {
        var exception = await Should.ThrowAsync<AuthenticationFailedException>(
            () => NewHandler().HandleAsync(new LoginCommand("not valid!", "whatever"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidCredentials);
        await _fixture.Users.DidNotReceive().GetByUsernameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _fixture.PasswordHasher.Received(1).SpendVerificationTime("whatever");
    }

    /// <summary>
    /// A wrong password fails generically and counts one failed attempt, without locking below the limit.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithWrongPassword_CountsTheFailure()
    {
        var account = IdentityFixture.NewUser();
        GivenAccount(account, passwordMatches: false);
        _fixture.Users.RegisterFailedLoginAsync(account.Id, Arg.Any<CancellationToken>()).Returns(User.MaxFailedLoginAttemptsAcrossAddresses - 1);
        _fixture.LoginFailures.RegisterFailureAsync(account.Id, Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(User.MaxFailedLoginAttempts - 1);

        var exception = await Should.ThrowAsync<AuthenticationFailedException>(
            () => NewHandler().HandleAsync(new LoginCommand("ana", "wrong"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidCredentials);
        await _fixture.Users.Received(1).RegisterFailedLoginAsync(account.Id, Arg.Any<CancellationToken>());
        await _fixture.LoginFailures.Received(1).RegisterFailureAsync(account.Id, Arg.Any<string>(), IdentityFixture.Now, Arg.Any<CancellationToken>());
        await _fixture.LoginFailures.DidNotReceive().BlockAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _fixture.Users.DidNotReceive().LockAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _fixture.RefreshTokens.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The failure that reaches the limit of an address blocks that address for the lockout duration, and does not lock the account:
    /// somebody who only knows the username cannot lock its owner out.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenTheLimitOfAnAddressIsReached_BlocksThatAddressOnly()
    {
        var account = IdentityFixture.NewUser();
        GivenAccount(account, passwordMatches: false);
        _fixture.LoginFailures.RegisterFailureAsync(account.Id, Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(User.MaxFailedLoginAttempts);
        _fixture.Users.RegisterFailedLoginAsync(account.Id, Arg.Any<CancellationToken>()).Returns(User.MaxFailedLoginAttempts);

        await Should.ThrowAsync<AuthenticationFailedException>(
            () => NewHandler().HandleAsync(new LoginCommand("ana", "wrong", "203.0.113.5"), TestContext.Current.CancellationToken));

        await _fixture.LoginFailures.Received(1).BlockAsync(
            account.Id,
            "code-hash-login-address:203.0.113.5",
            IdentityFixture.Now + User.LockoutDuration,
            Arg.Any<CancellationToken>());
        await _fixture.Users.DidNotReceive().LockAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Failures from many addresses together, enough that one address could not have made them, lock the account for every address.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenTheLimitAcrossAddressesIsReached_LocksTheAccount()
    {
        var account = IdentityFixture.NewUser(failedLoginAttempts: User.MaxFailedLoginAttemptsAcrossAddresses - 1);
        GivenAccount(account, passwordMatches: false);
        _fixture.Users.RegisterFailedLoginAsync(account.Id, Arg.Any<CancellationToken>()).Returns(User.MaxFailedLoginAttemptsAcrossAddresses);

        await Should.ThrowAsync<AuthenticationFailedException>(
            () => NewHandler().HandleAsync(new LoginCommand("ana", "wrong", "203.0.113.5"), TestContext.Current.CancellationToken));

        await _fixture.Users.Received(1).LockAsync(
            account.Id,
            IdentityFixture.Now + User.LockoutDuration,
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A blocked address is refused even with the right password, and says how long to wait, while the owner signing in from another
    /// address is not affected.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_FromABlockedAddress_RefusesWhileAnotherAddressStillSignsIn()
    {
        var account = IdentityFixture.NewUser();
        GivenAccount(account, passwordMatches: true);
        _fixture.LoginFailures.GetAsync(account.Id, "code-hash-login-address:198.51.100.7", Arg.Any<CancellationToken>())
            .Returns(new LoginAddressFailure(account.Id, "code-hash-login-address:198.51.100.7", 0, IdentityFixture.Now.AddMinutes(10)));

        var exception = await Should.ThrowAsync<AccountLockedException>(
            () => NewHandler().HandleAsync(new LoginCommand("ana", "correct horse", "198.51.100.7"), TestContext.Current.CancellationToken));
        var session = await NewHandler().HandleAsync(new LoginCommand("ana", "correct horse", "203.0.113.5"), TestContext.Current.CancellationToken);

        exception.RetryAfter.ShouldBe(TimeSpan.FromMinutes(10));
        session.PhotographerId.ShouldBe(account.Id);
        _fixture.PasswordHasher.Received(1).Verify(account.PasswordHash, "correct horse");
    }

    /// <summary>
    /// A block that already ended no longer refuses the address.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_FromAnAddressWhoseBlockEnded_SignsIn()
    {
        var account = IdentityFixture.NewUser();
        GivenAccount(account, passwordMatches: true);
        _fixture.LoginFailures.GetAsync(account.Id, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LoginAddressFailure(account.Id, "key", 0, IdentityFixture.Now.AddSeconds(-1)));

        var session = await NewHandler().HandleAsync(new LoginCommand("ana", "correct horse", "203.0.113.5"), TestContext.Current.CancellationToken);

        session.PhotographerId.ShouldBe(account.Id);
    }

    /// <summary>
    /// A good login forgets the failures of its address.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithTheRightPassword_ForgetsTheFailuresOfTheAddress()
    {
        var account = IdentityFixture.NewUser();
        GivenAccount(account, passwordMatches: true);

        await NewHandler().HandleAsync(new LoginCommand("ana", "correct horse", "203.0.113.5"), TestContext.Current.CancellationToken);

        await _fixture.LoginFailures.Received(1).DeleteAsync(account.Id, "code-hash-login-address:203.0.113.5", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A locked user refuses even the correct password and says how long to wait.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithALockedAccount_RefusesWithoutVerifyingThePassword()
    {
        var account = IdentityFixture.NewUser(lockedUntil: IdentityFixture.Now.AddMinutes(10));
        GivenAccount(account, passwordMatches: true);

        var exception = await Should.ThrowAsync<AccountLockedException>(
            () => NewHandler().HandleAsync(new LoginCommand("ana", "correct horse"), TestContext.Current.CancellationToken));

        exception.RetryAfter.ShouldBe(TimeSpan.FromMinutes(10));
        exception.Code.ShouldBe(ApplicationErrorCodes.AccountLocked);
        _fixture.PasswordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        await _fixture.RefreshTokens.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Once the lock has passed, the user accepts the correct password again.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_AfterTheLockEnds_AcceptsTheCorrectPassword()
    {
        var account = IdentityFixture.NewUser(lockedUntil: IdentityFixture.Now.AddSeconds(-1));
        GivenAccount(account, passwordMatches: true);

        var session = await NewHandler().HandleAsync(new LoginCommand("ana", "correct horse"), TestContext.Current.CancellationToken);

        session.PhotographerId.ShouldBe(account.Id);
        await _fixture.Users.Received(1).ResetFailedLoginsAsync(account.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A missing password is treated as a wrong one, not as a crash.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithANullPassword_FailsGenerically()
    {
        var account = IdentityFixture.NewUser();
        GivenAccount(account, passwordMatches: false);

        await Should.ThrowAsync<AuthenticationFailedException>(
            () => NewHandler().HandleAsync(new LoginCommand("ana", null!), TestContext.Current.CancellationToken));

        _fixture.PasswordHasher.Received(1).Verify(account.PasswordHash, string.Empty);
    }

    /// <summary>
    /// Makes the repository return the user and the hasher accept or reject the password.
    /// </summary>
    /// <param name="account">Stored user.</param>
    /// <param name="passwordMatches">Whether the password verifies.</param>
    private void GivenAccount(User account, bool passwordMatches)
    {
        _fixture.Users.GetByUsernameAsync(account.Username, Arg.Any<CancellationToken>()).Returns(account);
        _fixture.PasswordHasher.Verify(account.PasswordHash, Arg.Any<string>()).Returns(passwordMatches);
    }

    /// <summary>
    /// Creates the handler under test.
    /// </summary>
    /// <returns>The handler.</returns>
    private LoginHandler NewHandler() => new(
        _fixture.Credentials,
        _fixture.RefreshTokens,
        _fixture.Sessions,
        _fixture.Clock);
}
