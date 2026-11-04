using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Identity.ChangeEmail;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.UnitTests.Identity;

/// <summary>
/// Tests of <see cref="ChangeUnverifiedEmailHandler"/>: correcting a misspelled email before it is verified.
/// </summary>
public sealed class ChangeUnverifiedEmailHandlerTests
{
    private readonly IdentityFixture _fixture = new(verificationRequired: true);

    /// <summary>
    /// The email is replaced and the code goes to the new address, not to the old one.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Change_ReplacesTheEmailAndSendsTheCodeToTheNewAddress()
    {
        var user = GivenAUser();
        GivenTheEmailChangesTo(user, "ana.perez@example.com");

        var response = await NewHandler().HandleAsync(new ChangeUnverifiedEmailCommand(user.Id, "  Ana.Perez@Example.com "), TestContext.Current.CancellationToken);

        response.MaskedEmail.ShouldBe("a***@example.com");
        await _fixture.Users.Received(1).ChangeUnverifiedEmailAsync(user.Id, "ana.perez@example.com", Arg.Any<CancellationToken>());
        await _fixture.Codes.Received(1).SaveAsync(Arg.Is<EmailVerification>(record => record.UserId == user.Id), Arg.Any<CancellationToken>());
        var message = _fixture.Email.ReceivedCalls().Select(call => (EmailMessage)call.GetArguments()[0]!).ShouldHaveSingleItem();
        message.To.ShouldBe("ana.perez@example.com");
    }

    /// <summary>
    /// Typing the address the account already has does not write anything; it only sends the code again.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Change_ToTheSameAddress_OnlySendsTheCode()
    {
        var user = GivenAUser();

        await NewHandler().HandleAsync(new ChangeUnverifiedEmailCommand(user.Id, user.Email.ToUpperInvariant()), TestContext.Current.CancellationToken);

        await _fixture.Users.DidNotReceive().ChangeUnverifiedEmailAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _fixture.Email.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An address without a valid shape is refused before anything is read or written.
    /// </summary>
    /// <param name="email">Address typed.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("a@b")]
    public async Task Change_ToAnInvalidAddress_IsRefused(string email)
    {
        var user = GivenAUser();

        var exception = await Should.ThrowAsync<DomainException>(() =>
            NewHandler().HandleAsync(new ChangeUnverifiedEmailCommand(user.Id, email), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidEmail);
        await _fixture.Users.DidNotReceive().ChangeUnverifiedEmailAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Once the email is verified it can no longer be replaced with a session alone.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Change_ForAVerifiedAccount_IsAConflict()
    {
        var user = IdentityFixture.NewVerifiedUser();
        _fixture.Users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            NewHandler().HandleAsync(new ChangeUnverifiedEmailCommand(user.Id, "other@example.com"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.EmailAlreadyVerified);
        await _fixture.Users.DidNotReceive().ChangeUnverifiedEmailAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// If the account got verified between the read and the write, the write reports it and nothing is sent.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Change_WhenVerifiedMeanwhile_IsAConflictAndSendsNothing()
    {
        var user = GivenAUser();
        _fixture.Users.ChangeUnverifiedEmailAsync(user.Id, "other@example.com", Arg.Any<CancellationToken>()).Returns(false);

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            NewHandler().HandleAsync(new ChangeUnverifiedEmailCommand(user.Id, "other@example.com"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.EmailAlreadyVerified);
        await _fixture.Email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An address another account uses is refused by the store and nothing is sent.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Change_ToAnAddressInUse_IsAConflictAndSendsNothing()
    {
        var user = GivenAUser();
        _fixture.Users.ChangeUnverifiedEmailAsync(user.Id, "taken@example.com", Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new ConflictException(ApplicationErrorCodes.EmailTaken, "taken"));

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            NewHandler().HandleAsync(new ChangeUnverifiedEmailCommand(user.Id, "taken@example.com"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.EmailTaken);
        await _fixture.Email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Inside the cooldown of the previous code the change is refused <b>without</b> touching the account, so the limits cannot be
    /// skipped by changing the address.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Change_InsideTheCooldown_IsRateLimitedAndLeavesTheEmailAlone()
    {
        var user = GivenAUser();
        _fixture.Codes.GetAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(EmailVerification.Issue(user.Id, "old", null, IdentityFixture.Now.AddSeconds(-20)));

        var exception = await Should.ThrowAsync<OtpRateLimitedException>(() =>
            NewHandler().HandleAsync(new ChangeUnverifiedEmailCommand(user.Id, "other@example.com"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.EmailCodeRateLimited);
        exception.RetryAfter.ShouldBe(TimeSpan.FromSeconds(40));
        await _fixture.Users.DidNotReceive().ChangeUnverifiedEmailAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _fixture.Email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// After five codes in the hour the change is refused too, even when the last one is old enough.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Change_AfterTheHourlyLimit_IsRateLimited()
    {
        var user = GivenAUser();
        var record = new EmailVerification(
            user.Id, "old", IdentityFixture.Now.AddMinutes(-10), IdentityFixture.Now.AddMinutes(-20), 0, EmailVerification.MaxSendsPerWindow, IdentityFixture.Now.AddMinutes(-40));
        _fixture.Codes.GetAsync(user.Id, Arg.Any<CancellationToken>()).Returns(record);

        await Should.ThrowAsync<OtpRateLimitedException>(() =>
            NewHandler().HandleAsync(new ChangeUnverifiedEmailCommand(user.Id, "other@example.com"), TestContext.Current.CancellationToken));

        await _fixture.Users.DidNotReceive().ChangeUnverifiedEmailAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A user who no longer exists gets an authentication failure.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Change_ForAnUnknownUser_FailsAuthentication()
    {
        _fixture.Users.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        await Should.ThrowAsync<AuthenticationFailedException>(() =>
            NewHandler().HandleAsync(new ChangeUnverifiedEmailCommand(Guid.CreateVersion7(), "other@example.com"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Stores an unverified user so the handler finds it.
    /// </summary>
    /// <returns>The user.</returns>
    private User GivenAUser()
    {
        var user = IdentityFixture.NewUser();
        _fixture.Users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _fixture.Users.ChangeUnverifiedEmailAsync(user.Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        return user;
    }

    /// <summary>
    /// Makes the store answer with the user as it is after the email changed, as the second read of the handler does.
    /// </summary>
    /// <param name="user">User before the change.</param>
    /// <param name="newEmail">The new address.</param>
    private void GivenTheEmailChangesTo(User user, string newEmail)
    {
        var changed = User.Restore(user.Id, user.Version + 1, user.Username, newEmail, user.PasswordHash, user.Name, user.Phone, user.CreatedAt, 0, null);
        _fixture.Users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user, changed);
    }

    /// <summary>
    /// Builds the handler.
    /// </summary>
    /// <returns>The handler.</returns>
    private ChangeUnverifiedEmailHandler NewHandler() => new(_fixture.Users, _fixture.Codes, _fixture.VerificationSender, _fixture.Clock);
}
