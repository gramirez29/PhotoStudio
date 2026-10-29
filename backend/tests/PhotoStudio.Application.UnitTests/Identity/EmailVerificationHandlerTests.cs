using Microsoft.Extensions.Logging.Abstractions;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Identity;
using PhotoStudio.Application.Identity.Register;
using PhotoStudio.Application.Identity.ResendEmailVerification;
using PhotoStudio.Application.Identity.VerifyEmail;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.UnitTests.Identity;

/// <summary>
/// Tests of <see cref="EmailVerificationSettings"/>, <see cref="ResendEmailVerificationHandler"/> and <see cref="VerifyEmailHandler"/>.
/// </summary>
public sealed class EmailVerificationHandlerTests
{
    private readonly IdentityFixture _fixture = new(verificationRequired: true);

    /// <summary>
    /// Only a user who proved their email, or has none, is let in while verification is required.
    /// </summary>
    [Fact]
    public void Settings_LetInOnlyAVerifiedUserOrOneWithoutEmail()
    {
        var required = new EmailVerificationSettings(true);
        var off = new EmailVerificationSettings(false);
        var unverified = IdentityFixture.NewUser();
        var verified = User.Restore(unverified.Id, 1, "ana", "ana@example.com", "hash", "Ana", "70189220", IdentityFixture.Now, 0, null, false, IdentityFixture.Now);
        var withoutEmail = User.Restore(Guid.CreateVersion7(), 1, "old", string.Empty, "hash", "Old", "70189220", IdentityFixture.Now, 0, null);

        required.IsSatisfiedBy(unverified).ShouldBeFalse();
        required.IsSatisfiedBy(verified).ShouldBeTrue();
        required.IsSatisfiedBy(withoutEmail).ShouldBeTrue();
        off.IsSatisfiedBy(unverified).ShouldBeTrue();
    }

    /// <summary>
    /// A session of an unverified user says so, and the access token is issued as not verified.
    /// </summary>
    [Fact]
    public void Session_OfAnUnverifiedUser_SaysTheEmailIsNotVerified()
    {
        var session = _fixture.Sessions.Issue(IdentityFixture.NewUser(), Guid.CreateVersion7(), IdentityFixture.Now);

        session.Response.EmailVerified.ShouldBeFalse();
        _fixture.AccessTokens.Received(1).Issue(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Is<bool>(isSystemOperator => !isSystemOperator), Arg.Is<bool>(emailVerified => !emailVerified), Arg.Any<DateTimeOffset>());
    }

    /// <summary>
    /// Asking for a code stores its hash and emails the code to the address of the account.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Resend_StoresTheHashAndEmailsTheCode()
    {
        var user = GivenAUser();

        var response = await NewResendHandler().HandleAsync(new ResendEmailVerificationCommand(user.Id), TestContext.Current.CancellationToken);

        response.MaskedEmail.ShouldBe("a***@example.com");
        response.ExpiresAt.ShouldBe(IdentityFixture.Now + EmailVerification.Lifetime);
        await _fixture.Codes.Received(1).SaveAsync(Arg.Is<EmailVerification>(record => record.UserId == user.Id && record.CodeHash == "code-hash-123456"), Arg.Any<CancellationToken>());
        var message = _fixture.Email.ReceivedCalls().Select(call => (EmailMessage)call.GetArguments()[0]!).ShouldHaveSingleItem();
        message.To.ShouldBe(user.Email);
        message.TextBody.ShouldContain("123456");
    }

    /// <summary>
    /// A second code asked for right away is refused with the time to wait, and nothing is sent.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Resend_InsideTheCooldown_IsRateLimited()
    {
        var user = GivenAUser();
        _fixture.Codes.GetAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(EmailVerification.Issue(user.Id, "old", null, IdentityFixture.Now.AddSeconds(-20)));

        var exception = await Should.ThrowAsync<OtpRateLimitedException>(() =>
            NewResendHandler().HandleAsync(new ResendEmailVerificationCommand(user.Id), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.EmailCodeRateLimited);
        exception.RetryAfter.ShouldBe(TimeSpan.FromSeconds(40));
        await _fixture.Email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// There is nothing to send to an account whose email is already verified, or that has no email.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Resend_ForAVerifiedAccountOrOneWithoutEmail_IsAConflict()
    {
        var verified = User.Restore(Guid.CreateVersion7(), 1, "ana", "ana@example.com", "hash", "Ana", "70189220", IdentityFixture.Now, 0, null, false, IdentityFixture.Now);
        var withoutEmail = User.Restore(Guid.CreateVersion7(), 1, "old", string.Empty, "hash", "Old", "70189220", IdentityFixture.Now, 0, null);
        _fixture.Users.GetByIdAsync(verified.Id, Arg.Any<CancellationToken>()).Returns(verified);
        _fixture.Users.GetByIdAsync(withoutEmail.Id, Arg.Any<CancellationToken>()).Returns(withoutEmail);

        var alreadyVerified = await Should.ThrowAsync<ConflictException>(() =>
            NewResendHandler().HandleAsync(new ResendEmailVerificationCommand(verified.Id), TestContext.Current.CancellationToken));
        var noEmail = await Should.ThrowAsync<ConflictException>(() =>
            NewResendHandler().HandleAsync(new ResendEmailVerificationCommand(withoutEmail.Id), TestContext.Current.CancellationToken));

        alreadyVerified.Code.ShouldBe(ApplicationErrorCodes.EmailAlreadyVerified);
        noEmail.Code.ShouldBe(ApplicationErrorCodes.AccountEmailMissing);
    }

    /// <summary>
    /// A user who no longer exists gets an authentication failure.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Resend_ForAnUnknownUser_FailsAuthentication()
    {
        _fixture.Users.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        await Should.ThrowAsync<AuthenticationFailedException>(() =>
            NewResendHandler().HandleAsync(new ResendEmailVerificationCommand(Guid.CreateVersion7()), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The right code marks the email as verified, spends the code, closes the other sessions and answers with a new session
    /// whose token lets the user in.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Verify_WithTheRightCode_VerifiesAndIssuesANewSession()
    {
        var user = GivenAUser();
        GivenACode(user, "123456");
        var verified = User.Restore(user.Id, 2, user.Username, user.Email, user.PasswordHash, user.Name, user.Phone, user.CreatedAt, 0, null, false, IdentityFixture.Now);
        _fixture.Users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user, verified);

        var response = await NewVerifyHandler().HandleAsync(new VerifyEmailCommand(user.Id, " 123456 "), TestContext.Current.CancellationToken);

        response.EmailVerified.ShouldBeTrue();
        await _fixture.Users.Received(1).MarkEmailVerifiedAsync(user.Id, IdentityFixture.Now, Arg.Any<CancellationToken>());
        await _fixture.Codes.Received(1).DeleteAsync(user.Id, Arg.Any<CancellationToken>());
        await _fixture.RefreshTokens.Received(1).RevokeAllForUserAsync(user.Id, IdentityFixture.Now, Arg.Any<CancellationToken>());
        await _fixture.RefreshTokens.Received(1).AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
        _fixture.AccessTokens.Received(1).Issue(user.Id, user.Username, false, true, IdentityFixture.Now);
    }

    /// <summary>
    /// A wrong code, a code that is not six digits and a user with no code all answer the same, and verify nothing.
    /// </summary>
    /// <param name="typed">What the user typed.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("654321")]
    [InlineData("12345")]
    [InlineData("12345a")]
    [InlineData("")]
    public async Task Verify_WithAWrongCode_IsRejectedWithoutSayingWhy(string typed)
    {
        var user = GivenAUser();
        GivenACode(user, "123456");

        var exception = await Should.ThrowAsync<DomainException>(() =>
            NewVerifyHandler().HandleAsync(new VerifyEmailCommand(user.Id, typed), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidEmailCode);
        await _fixture.Users.DidNotReceive().MarkEmailVerifiedAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Each guess is counted before it is compared, and the fifth wrong one spends the code.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Verify_TheFifthWrongGuess_SpendsTheCode()
    {
        var user = GivenAUser();
        _fixture.Codes.RegisterAttemptAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(EmailVerification.Issue(user.Id, "code-hash-123456", null, IdentityFixture.Now) with { FailedAttempts = EmailVerification.MaxFailedAttempts });

        await Should.ThrowAsync<DomainException>(() =>
            NewVerifyHandler().HandleAsync(new VerifyEmailCommand(user.Id, "000000"), TestContext.Current.CancellationToken));

        await _fixture.Codes.Received(1).DeleteAsync(user.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An expired code, or one that already took more guesses than allowed, is refused even if it is right.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Verify_WithAnExpiredCode_IsRejectedAndRemoved()
    {
        var user = GivenAUser();
        _fixture.Codes.RegisterAttemptAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(EmailVerification.Issue(user.Id, "code-hash-123456", null, IdentityFixture.Now - EmailVerification.Lifetime - TimeSpan.FromSeconds(1)) with { FailedAttempts = 1 });

        var exception = await Should.ThrowAsync<DomainException>(() =>
            NewVerifyHandler().HandleAsync(new VerifyEmailCommand(user.Id, "123456"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidEmailCode);
        await _fixture.Codes.Received(1).DeleteAsync(user.Id, Arg.Any<CancellationToken>());
        await _fixture.Users.DidNotReceive().MarkEmailVerifiedAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An account that is already verified gets a fresh session without needing a code, and nothing is written again.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Verify_ForAnAlreadyVerifiedAccount_IssuesASessionWithoutACode()
    {
        var verified = User.Restore(Guid.CreateVersion7(), 1, "ana", "ana@example.com", "hash", "Ana", "70189220", IdentityFixture.Now, 0, null, false, IdentityFixture.Now);
        _fixture.Users.GetByIdAsync(verified.Id, Arg.Any<CancellationToken>()).Returns(verified);

        var response = await NewVerifyHandler().HandleAsync(new VerifyEmailCommand(verified.Id, "anything"), TestContext.Current.CancellationToken);

        response.EmailVerified.ShouldBeTrue();
        await _fixture.Users.DidNotReceive().MarkEmailVerifiedAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _fixture.Codes.DidNotReceive().RegisterAttemptAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Registering with verification required sends the code to the new address, and the session says the email is not verified.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_WhenVerificationIsRequired_SendsTheCodeAndTheSessionIsNotVerified()
    {
        var response = await NewRegisterHandler(_fixture).HandleAsync(NewRegistration(), TestContext.Current.CancellationToken);

        response.EmailVerified.ShouldBeFalse();
        var message = _fixture.Email.ReceivedCalls().Select(call => (EmailMessage)call.GetArguments()[0]!).ShouldHaveSingleItem();
        message.To.ShouldBe("ana@example.com");
        message.TextBody.ShouldContain("123456");
        await _fixture.Codes.Received(1).SaveAsync(Arg.Any<EmailVerification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// If the email cannot be sent the account is still created, so the user can ask for another code from the app.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_WhenTheEmailFails_StillCreatesTheAccount()
    {
        _fixture.Email.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new EmailDeliveryException("provider down")));

        var response = await NewRegisterHandler(_fixture).HandleAsync(NewRegistration(), TestContext.Current.CancellationToken);

        response.EmailVerified.ShouldBeFalse();
        await _fixture.Users.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// With verification turned off nothing is sent, and the session lets the user into the app.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_WhenVerificationIsNotRequired_SendsNothingAndTheSessionIsVerified()
    {
        var off = new IdentityFixture(verificationRequired: false);

        var response = await NewRegisterHandler(off).HandleAsync(NewRegistration(), TestContext.Current.CancellationToken);

        response.EmailVerified.ShouldBeTrue();
        await off.Email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Builds a valid registration.
    /// </summary>
    /// <returns>The command.</returns>
    private static RegisterUserCommand NewRegistration() => new("ana", "ana@example.com", "correct horse battery", "Ana Pérez", "70189220");

    /// <summary>
    /// Builds the registration handler on the ports of a fixture.
    /// </summary>
    /// <param name="fixture">Fixture whose ports and settings the handler uses.</param>
    /// <returns>The handler.</returns>
    private static RegisterUserHandler NewRegisterHandler(IdentityFixture fixture) => new(
        fixture.Users,
        fixture.RefreshTokens,
        fixture.PasswordHasher,
        fixture.Sessions,
        new RegistrationSettings(true),
        fixture.Verification,
        fixture.VerificationSender,
        fixture.Clock,
        NullLogger<RegisterUserHandler>.Instance);

    /// <summary>
    /// Stores an unverified user in the substituted repository.
    /// </summary>
    /// <returns>The user.</returns>
    private User GivenAUser()
    {
        var user = IdentityFixture.NewUser();
        _fixture.Users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    /// <summary>
    /// Stores the code of a user, as <see cref="EmailVerificationSender"/> would have.
    /// </summary>
    /// <param name="user">User the code is for.</param>
    /// <param name="code">The code in clear text.</param>
    private void GivenACode(User user, string code) =>
        _fixture.Codes.RegisterAttemptAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(EmailVerification.Issue(user.Id, "code-hash-" + code, null, IdentityFixture.Now) with { FailedAttempts = 1 });

    /// <summary>
    /// Builds the handler that asks for a new code.
    /// </summary>
    /// <returns>The handler.</returns>
    private ResendEmailVerificationHandler NewResendHandler() => new(_fixture.Users, _fixture.VerificationSender, _fixture.Clock);

    /// <summary>
    /// Builds the handler that verifies the code.
    /// </summary>
    /// <returns>The handler.</returns>
    private VerifyEmailHandler NewVerifyHandler() =>
        new(_fixture.Users, _fixture.Codes, _fixture.CodeService, _fixture.RefreshTokens, _fixture.Sessions, _fixture.Clock);
}
