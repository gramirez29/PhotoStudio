using Microsoft.Extensions.Logging.Abstractions;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Identity.RecoverPassword;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.UnitTests.Identity;

/// <summary>
/// Tests of <see cref="RequestPasswordResetHandler"/> and <see cref="ConfirmPasswordResetHandler"/>: choosing a new password
/// after forgetting the old one, with a code sent to the verified email.
/// </summary>
public sealed class PasswordRecoveryHandlerTests
{
    private const string Code = "123456";

    private readonly IdentityFixture _fixture = new();
    private readonly IPasswordResetRepository _codes = Substitute.For<IPasswordResetRepository>();

    /// <summary>
    /// A verified account found by username gets the code at its email, stored as a hash of the code with the purpose in front.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Request_ByUsername_StoresTheHashAndEmailsTheCode()
    {
        var user = GivenAVerifiedUser();

        await NewRequestHandler().HandleAsync(new RequestPasswordResetCommand(" ANA "), TestContext.Current.CancellationToken);

        await _codes.Received(1).SaveAsync(
            Arg.Is<PasswordResetCode>(record => record.UserId == user.Id && record.CodeHash == "code-hash-password-reset:123456" && record.ExpiresAt == IdentityFixture.Now + PasswordResetCode.Lifetime),
            Arg.Any<CancellationToken>());
        var message = _fixture.Email.ReceivedCalls().Select(call => (EmailMessage)call.GetArguments()[0]!).ShouldHaveSingleItem();
        message.To.ShouldBe(user.Email);
        message.TextBody.ShouldContain(Code);
    }

    /// <summary>
    /// Typing the email finds the account too.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Request_ByEmail_FindsTheAccount()
    {
        var user = GivenAVerifiedUser();

        await NewRequestHandler().HandleAsync(new RequestPasswordResetCommand("  Ana@Example.com "), TestContext.Current.CancellationToken);

        await _fixture.Users.Received(1).GetByEmailAsync("ana@example.com", Arg.Any<CancellationToken>());
        await _codes.Received(1).SaveAsync(Arg.Is<PasswordResetCode>(record => record.UserId == user.Id), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An unknown account, an unverified one, nothing typed and a text that is not a username get the same answer and nothing is sent,
    /// so the answer does not say which accounts exist.
    /// </summary>
    /// <param name="identifier">What was typed.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("nobody")]
    [InlineData("nobody@example.com")]
    [InlineData("unverified")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("not a username!")]
    public async Task Request_ForAnAccountThatCannotRecover_SendsNothingAndAnswersTheSame(string identifier)
    {
        var unverified = IdentityFixture.NewUser();
        _fixture.Users.GetByUsernameAsync("unverified", Arg.Any<CancellationToken>()).Returns(unverified);

        var response = await NewRequestHandler().HandleAsync(new RequestPasswordResetCommand(identifier), TestContext.Current.CancellationToken);

        response.ShouldBe(new PasswordResetRequestedResponse());
        await _codes.DidNotReceive().SaveAsync(Arg.Any<PasswordResetCode>(), Arg.Any<CancellationToken>());
        await _fixture.Email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An account with no email on file cannot recover by email.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Request_ForAnAccountWithoutEmail_SendsNothing()
    {
        var old = User.Restore(Guid.CreateVersion7(), 1, "old", string.Empty, "hash", "Old", "70189220", IdentityFixture.Now, 0, null, false, IdentityFixture.Now);
        _fixture.Users.GetByUsernameAsync("old", Arg.Any<CancellationToken>()).Returns(old);

        await NewRequestHandler().HandleAsync(new RequestPasswordResetCommand("old"), TestContext.Current.CancellationToken);

        await _fixture.Email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Inside the cooldown or after the hourly limit nothing is sent and the answer does not change: a refusal would say that the
    /// account exists.
    /// </summary>
    /// <param name="secondsSinceTheLastCode">How long ago the previous code was sent.</param>
    /// <param name="sentInTheWindow">Codes sent in the current hour.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData(20, 1)]
    [InlineData(300, PasswordResetCode.MaxSendsPerWindow)]
    public async Task Request_WhenTheLimitsApply_SendsNothingAndAnswersTheSame(int secondsSinceTheLastCode, int sentInTheWindow)
    {
        var user = GivenAVerifiedUser();
        var sentAt = IdentityFixture.Now.AddSeconds(-secondsSinceTheLastCode);
        _codes.GetAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(new PasswordResetCode(user.Id, "old", sentAt + PasswordResetCode.Lifetime, sentAt, 0, sentInTheWindow, sentAt.AddMinutes(-10)));

        var response = await NewRequestHandler().HandleAsync(new RequestPasswordResetCommand("ana"), TestContext.Current.CancellationToken);

        response.ShouldBe(new PasswordResetRequestedResponse());
        await _codes.DidNotReceive().SaveAsync(Arg.Any<PasswordResetCode>(), Arg.Any<CancellationToken>());
        await _fixture.Email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A provider that fails is not reported to the caller: a failure that only happens for real accounts would reveal them.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Request_WhenTheEmailFails_StillAnswersTheSame()
    {
        GivenAVerifiedUser();
        _fixture.Email.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns<Task>(_ => throw new EmailDeliveryException("down"));

        var response = await NewRequestHandler().HandleAsync(new RequestPasswordResetCommand("ana"), TestContext.Current.CancellationToken);

        response.ShouldBe(new PasswordResetRequestedResponse());
    }

    /// <summary>
    /// The right code stores the new password, spends the code, closes every session, cuts off the web area and tells the owner.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Confirm_WithTheRightCode_ChangesThePasswordAndClosesEverySession()
    {
        var user = GivenAVerifiedUser();
        GivenTheCode(user);

        var response = await NewConfirmHandler().HandleAsync(new ConfirmPasswordResetCommand("ana", Code, "a brand new password"), TestContext.Current.CancellationToken);

        response.ShouldBe(new PasswordResetResponse());
        await _fixture.Users.Received(1).ChangePasswordAsync(user.Id, user.PasswordHash, "hashed-password", Arg.Any<CancellationToken>());
        await _codes.Received(1).DeleteAsync(user.Id, Arg.Any<CancellationToken>());
        await _fixture.RefreshTokens.Received(1).RevokeAllForUserAsync(user.Id, IdentityFixture.Now, Arg.Any<CancellationToken>());
        await _fixture.StudioSessions.Received(1).DeleteAllForUserAsync(user.Id, Arg.Any<CancellationToken>());
        await _fixture.StudioDevices.Received(1).DeleteAllForUserAsync(user.Id, Arg.Any<CancellationToken>());
        await _fixture.StudioChallenges.Received(1).DeleteAsync(user.Id, Arg.Any<CancellationToken>());
        var notice = _fixture.Email.ReceivedCalls().Select(call => (EmailMessage)call.GetArguments()[0]!).ShouldHaveSingleItem();
        notice.To.ShouldBe(user.Email);
        notice.Subject.ShouldContain("cambió");
        notice.TextBody.ShouldNotContain("a brand new password");
    }

    /// <summary>
    /// Nobody is signed in by recovering: no session is issued.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Confirm_DoesNotIssueAnySession()
    {
        var user = GivenAVerifiedUser();
        GivenTheCode(user);

        await NewConfirmHandler().HandleAsync(new ConfirmPasswordResetCommand("ana", Code, "a brand new password"), TestContext.Current.CancellationToken);

        await _fixture.RefreshTokens.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A wrong code is refused with the same error as every other failure, and counts a guess; the fifth wrong guess spends the code.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Confirm_WithAWrongCode_IsRefusedAndTheFifthGuessSpendsTheCode()
    {
        var user = GivenAVerifiedUser();
        GivenTheCode(user, failedAttempts: PasswordResetCode.MaxFailedAttempts);

        var exception = await Should.ThrowAsync<DomainException>(() =>
            NewConfirmHandler().HandleAsync(new ConfirmPasswordResetCommand("ana", "000000", "a brand new password"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidPasswordResetCode);
        await _fixture.Users.DidNotReceive().ChangePasswordAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _codes.Received(1).DeleteAsync(user.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A code made for another purpose does not work: the hash includes the purpose.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Confirm_WithAHashMadeWithoutThePurpose_IsRefused()
    {
        var user = GivenAVerifiedUser();
        _codes.RegisterAttemptAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(new PasswordResetCode(user.Id, "code-hash-123456", IdentityFixture.Now.AddMinutes(5), IdentityFixture.Now, 1, 1, IdentityFixture.Now));

        await Should.ThrowAsync<DomainException>(() =>
            NewConfirmHandler().HandleAsync(new ConfirmPasswordResetCommand("ana", Code, "a brand new password"), TestContext.Current.CancellationToken));

        await _fixture.Users.DidNotReceive().ChangePasswordAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Without a code, with an expired one or with one that already took too many guesses, the answer is the same and the record is cleaned.
    /// </summary>
    /// <param name="scenario">What is wrong with the stored code.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("none")]
    [InlineData("expired")]
    [InlineData("spent")]
    public async Task Confirm_WithoutAUsableCode_IsRefused(string scenario)
    {
        var user = GivenAVerifiedUser();
        PasswordResetCode? stored = scenario switch
        {
            "expired" => new PasswordResetCode(user.Id, "code-hash-password-reset:123456", IdentityFixture.Now.AddMinutes(-1), IdentityFixture.Now.AddMinutes(-11), 1, 1, IdentityFixture.Now.AddMinutes(-11)),
            "spent" => new PasswordResetCode(user.Id, "code-hash-password-reset:123456", IdentityFixture.Now.AddMinutes(5), IdentityFixture.Now, PasswordResetCode.MaxFailedAttempts + 1, 1, IdentityFixture.Now),
            _ => null,
        };
        _codes.RegisterAttemptAsync(user.Id, Arg.Any<CancellationToken>()).Returns(stored);

        var exception = await Should.ThrowAsync<DomainException>(() =>
            NewConfirmHandler().HandleAsync(new ConfirmPasswordResetCommand("ana", Code, "a brand new password"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidPasswordResetCode);
        await _fixture.Users.DidNotReceive().ChangePasswordAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        if (stored is not null)
        {
            await _codes.Received(1).DeleteAsync(user.Id, Arg.Any<CancellationToken>());
        }
    }

    /// <summary>
    /// An unknown account, an unverified one and a code that is not six digits get the same error, and no guess is counted.
    /// </summary>
    /// <param name="identifier">Account typed.</param>
    /// <param name="code">Code typed.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("nobody", Code)]
    [InlineData("unverified", Code)]
    [InlineData("ana", "12345")]
    [InlineData("ana", "12345a")]
    [InlineData("ana", "")]
    public async Task Confirm_WithAnAccountOrCodeThatCannotWork_GivesTheSameErrorAndCountsNoGuess(string identifier, string code)
    {
        GivenAVerifiedUser();
        _fixture.Users.GetByUsernameAsync("unverified", Arg.Any<CancellationToken>()).Returns(IdentityFixture.NewUser());

        var exception = await Should.ThrowAsync<DomainException>(() =>
            NewConfirmHandler().HandleAsync(new ConfirmPasswordResetCommand(identifier, code, "a brand new password"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidPasswordResetCode);
        await _codes.DidNotReceive().RegisterAttemptAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A password that is too short is refused before the code is looked at, so it does not spend a guess.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Confirm_WithAWeakPassword_IsRefusedWithoutCountingAGuess()
    {
        GivenAVerifiedUser();

        var exception = await Should.ThrowAsync<DomainException>(() =>
            NewConfirmHandler().HandleAsync(new ConfirmPasswordResetCommand("ana", Code, "short"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.WeakPassword);
        await _codes.DidNotReceive().RegisterAttemptAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// If the password changed between the read and the write, the second request is told to try again and nothing else happens.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Confirm_WhenThePasswordChangedMeanwhile_IsAConflict()
    {
        var user = GivenAVerifiedUser();
        GivenTheCode(user);
        _fixture.Users.ChangePasswordAsync(user.Id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            NewConfirmHandler().HandleAsync(new ConfirmPasswordResetCommand("ana", Code, "a brand new password"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.ConcurrencyConflict);
        await _fixture.RefreshTokens.DidNotReceive().RevokeAllForUserAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The password is already changed when the notice is sent, so a provider that fails does not undo or fail the recovery.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Confirm_WhenTheNoticeFails_StillSucceeds()
    {
        var user = GivenAVerifiedUser();
        GivenTheCode(user);
        _fixture.Email.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns<Task>(_ => throw new EmailDeliveryException("down"));

        var response = await NewConfirmHandler().HandleAsync(new ConfirmPasswordResetCommand("ana", Code, "a brand new password"), TestContext.Current.CancellationToken);

        response.ShouldBe(new PasswordResetResponse());
    }

    /// <summary>
    /// Stores a verified user so the handlers find it by username and by email.
    /// </summary>
    /// <returns>The user.</returns>
    private User GivenAVerifiedUser()
    {
        var user = IdentityFixture.NewVerifiedUser();
        _fixture.Users.GetByUsernameAsync("ana", Arg.Any<CancellationToken>()).Returns(user);
        _fixture.Users.GetByEmailAsync("ana@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _fixture.Users.ChangePasswordAsync(user.Id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        return user;
    }

    /// <summary>
    /// Stores the code the user was sent, as the request handler would have: the hash of the purpose and the code.
    /// </summary>
    /// <param name="user">User the code is for.</param>
    /// <param name="failedAttempts">Guesses counted, this one included.</param>
    private void GivenTheCode(User user, int failedAttempts = 1) =>
        _codes.RegisterAttemptAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(new PasswordResetCode(user.Id, "code-hash-password-reset:123456", IdentityFixture.Now.AddMinutes(5), IdentityFixture.Now, failedAttempts, 1, IdentityFixture.Now));

    /// <summary>
    /// Builds the handler that sends the code.
    /// </summary>
    /// <returns>The handler.</returns>
    private RequestPasswordResetHandler NewRequestHandler() =>
        new(_fixture.Users, _codes, _fixture.CodeService, _fixture.Email, _fixture.Clock, NullLogger<RequestPasswordResetHandler>.Instance);

    /// <summary>
    /// Builds the handler that chooses the new password.
    /// </summary>
    /// <returns>The handler.</returns>
    private ConfirmPasswordResetHandler NewConfirmHandler() =>
        new(_fixture.Users, _codes, _fixture.CodeService, _fixture.PasswordHasher, _fixture.RefreshTokens, _fixture.StudioAccess, _fixture.Email, _fixture.Clock, NullLogger<ConfirmPasswordResetHandler>.Instance);
}
