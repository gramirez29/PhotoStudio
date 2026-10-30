using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Identity.ChangePassword;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.UnitTests.Identity;

/// <summary>
/// Tests of <see cref="ChangePasswordHandler"/>.
/// </summary>
public sealed class ChangePasswordHandlerTests
{
    private readonly IdentityFixture _fixture = new();

    /// <summary>
    /// With the right current password the hash is replaced, every old session is closed and a new one is issued.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithTheRightCurrentPassword_ChangesItAndReplacesTheSessions()
    {
        var account = GivenAccount(currentPasswordMatches: true);
        _fixture.Users.ChangePasswordAsync(account.Id, "stored-hash", "hashed-password", Arg.Any<CancellationToken>()).Returns(true);

        var session = await NewHandler().HandleAsync(
            new ChangePasswordCommand(account.Id, "old password", "a brand new password"),
            TestContext.Current.CancellationToken);

        session.PhotographerId.ShouldBe(account.Id);
        session.RefreshToken.ShouldBe("new-secret");
        await _fixture.Users.Received(1).ChangePasswordAsync(account.Id, "stored-hash", "hashed-password", Arg.Any<CancellationToken>());
        await _fixture.RefreshTokens.Received(1).RevokeAllForUserAsync(account.Id, IdentityFixture.Now, Arg.Any<CancellationToken>());
        await _fixture.RefreshTokens.Received(1).AddAsync(
            Arg.Is<RefreshToken>(token => token.PhotographerId == account.Id && token.TokenHash == "new-hash"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Changing the password also takes away what the web area trusts: its sessions, the browsers it remembered and any code it was
    /// waiting for, so whoever had the old password, or a copied cookie, is out.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_AlsoRevokesTheAccessOfTheWebArea()
    {
        var account = GivenAccount(currentPasswordMatches: true);
        _fixture.Users.ChangePasswordAsync(account.Id, "stored-hash", "hashed-password", Arg.Any<CancellationToken>()).Returns(true);

        await NewHandler().HandleAsync(new ChangePasswordCommand(account.Id, "old password", "a brand new password"), TestContext.Current.CancellationToken);

        await _fixture.StudioSessions.Received(1).DeleteAllForUserAsync(account.Id, Arg.Any<CancellationToken>());
        await _fixture.StudioDevices.Received(1).DeleteAllForUserAsync(account.Id, Arg.Any<CancellationToken>());
        await _fixture.StudioChallenges.Received(1).DeleteAsync(account.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When the password could not be changed, the web area keeps its sessions: nothing about the account changed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenThePasswordIsNotChanged_LeavesTheWebAreaAlone()
    {
        var account = GivenAccount(currentPasswordMatches: false);
        _fixture.Users.RegisterFailedLoginAsync(account.Id, Arg.Any<CancellationToken>()).Returns(1);

        await Should.ThrowAsync<DomainException>(() => NewHandler().HandleAsync(
            new ChangePasswordCommand(account.Id, "wrong", "a brand new password"), TestContext.Current.CancellationToken));

        await _fixture.StudioSessions.DidNotReceive().DeleteAllForUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _fixture.StudioDevices.DidNotReceive().DeleteAllForUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A wrong current password is rejected, counts as a failed login and changes nothing.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAWrongCurrentPassword_RejectsAndCountsTheFailure()
    {
        var account = GivenAccount(currentPasswordMatches: false);
        _fixture.Users.RegisterFailedLoginAsync(account.Id, Arg.Any<CancellationToken>()).Returns(1);

        var exception = await Should.ThrowAsync<DomainException>(() => NewHandler().HandleAsync(
            new ChangePasswordCommand(account.Id, "wrong", "a brand new password"),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidCurrentPassword);
        await _fixture.Users.DidNotReceive().ChangePasswordAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _fixture.RefreshTokens.DidNotReceive().RevokeAllForUserAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _fixture.Users.DidNotReceive().LockAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The fifth wrong current password locks the user, like the login does.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_AtTheFailureLimit_LocksTheUser()
    {
        var account = GivenAccount(currentPasswordMatches: false);
        _fixture.Users.RegisterFailedLoginAsync(account.Id, Arg.Any<CancellationToken>()).Returns(User.MaxFailedLoginAttempts);

        await Should.ThrowAsync<DomainException>(() => NewHandler().HandleAsync(
            new ChangePasswordCommand(account.Id, "wrong", "a brand new password"),
            TestContext.Current.CancellationToken));

        await _fixture.Users.Received(1).LockAsync(account.Id, IdentityFixture.Now + User.LockoutDuration, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A locked user cannot try passwords through this endpoint either.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhileLocked_RefusesWithoutVerifying()
    {
        var account = IdentityFixture.NewUser(lockedUntil: IdentityFixture.Now.AddMinutes(10));
        _fixture.Users.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var exception = await Should.ThrowAsync<AccountLockedException>(() => NewHandler().HandleAsync(
            new ChangePasswordCommand(account.Id, "old password", "a brand new password"),
            TestContext.Current.CancellationToken));

        exception.RetryAfter.ShouldBe(TimeSpan.FromMinutes(10));
        _fixture.PasswordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    /// <summary>
    /// A new password that is too short is rejected after the current one was verified.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAWeakNewPassword_IsRejected()
    {
        var account = GivenAccount(currentPasswordMatches: true);

        var exception = await Should.ThrowAsync<DomainException>(() => NewHandler().HandleAsync(
            new ChangePasswordCommand(account.Id, "old password", "short"),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.WeakPassword);
        await _fixture.Users.DidNotReceive().ChangePasswordAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Keeping the same password is rejected: it would close the other sessions for no change.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithTheSamePassword_IsRejected()
    {
        var account = GivenAccount(currentPasswordMatches: true);

        var exception = await Should.ThrowAsync<DomainException>(() => NewHandler().HandleAsync(
            new ChangePasswordCommand(account.Id, "old password", "old password"),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.WeakPassword);
    }

    /// <summary>
    /// When another request changed the password between the read and the write, the change is refused and no session is touched.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenThePasswordChangedMeanwhile_ConflictsWithoutClosingSessions()
    {
        var account = GivenAccount(currentPasswordMatches: true);
        _fixture.Users.ChangePasswordAsync(account.Id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var exception = await Should.ThrowAsync<ConflictException>(() => NewHandler().HandleAsync(
            new ChangePasswordCommand(account.Id, "old password", "a brand new password"),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.ConcurrencyConflict);
        await _fixture.RefreshTokens.DidNotReceive().RevokeAllForUserAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A token of a user that no longer exists is not accepted.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_ForAnUnknownUser_FailsAuthentication()
    {
        await Should.ThrowAsync<AuthenticationFailedException>(() => NewHandler().HandleAsync(
            new ChangePasswordCommand(Guid.CreateVersion7(), "old password", "a brand new password"),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Stores a user and sets whether the current password matches.
    /// </summary>
    /// <param name="currentPasswordMatches">Result of verifying the current password.</param>
    /// <returns>The stored user.</returns>
    private User GivenAccount(bool currentPasswordMatches)
    {
        var account = IdentityFixture.NewUser();
        _fixture.Users.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        _fixture.PasswordHasher.Verify("stored-hash", Arg.Any<string>()).Returns(currentPasswordMatches);
        return account;
    }

    /// <summary>
    /// Builds the handler on the substituted ports.
    /// </summary>
    /// <returns>The handler.</returns>
    private ChangePasswordHandler NewHandler() =>
        new(_fixture.Users, _fixture.RefreshTokens, _fixture.PasswordHasher, _fixture.Sessions, _fixture.StudioAccess, _fixture.Clock);
}
