using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Identity.Studio;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.UnitTests.Identity;

/// <summary>
/// Tests of the web sign-in: the password step, the code step, the resend, the sign-out and the check of the session on every request.
/// </summary>
public sealed class StudioHandlerTests
{
    private const string ExpectedCodeHash = "code-hash-studio-login:123456";

    private readonly IdentityFixture _fixture = new();
    private readonly User _user = IdentityFixture.NewVerifiedUser();

    /// <summary>
    /// Initializes a new instance of the <see cref="StudioHandlerTests"/> class with a user whose password is right.
    /// </summary>
    public StudioHandlerTests()
    {
        _fixture.Users.GetByUsernameAsync("ana", Arg.Any<CancellationToken>()).Returns(_user);
        _fixture.Users.GetByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
        _fixture.PasswordHasher.Verify("stored-hash", "right password").Returns(true);
        _fixture.StudioChallenges.DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    /// <summary>
    /// A right password in a browser that is not remembered sends a code to the email of the account and starts no session.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Login_InANewBrowser_SendsTheCodeAndStartsNoSession()
    {
        var result = await Login().HandleAsync(new StudioLoginCommand("ana", "right password", null), TestContext.Current.CancellationToken);

        var required = result.ShouldBeOfType<StudioCodeRequired>();
        required.ChallengeToken.ShouldBe("new-secret");
        required.MaskedEmail.ShouldNotBe("ana@example.com");
        required.ExpiresAt.ShouldBe(IdentityFixture.Now.AddMinutes(10));
        await _fixture.StudioChallenges.Received(1).SaveAsync(
            Arg.Is<StudioLoginChallenge>(challenge => challenge.UserId == _user.Id && challenge.TokenHash == "new-hash" && challenge.CodeHash == ExpectedCodeHash),
            Arg.Any<CancellationToken>());
        await _fixture.Email.Received(1).SendAsync(
            Arg.Is<EmailMessage>(message => message.To == "ana@example.com" && message.TextBody.Contains("123456") && message.Subject.Contains("código")),
            Arg.Any<CancellationToken>());
        await _fixture.StudioSessions.DidNotReceive().AddAsync(Arg.Any<StudioSession>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The code is hashed with its own purpose, so it can never match a code made for something else.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Login_HashesTheCodeWithItsPurpose()
    {
        await Login().HandleAsync(new StudioLoginCommand("ana", "right password", null), TestContext.Current.CancellationToken);

        _fixture.CodeService.Received().Hash(_user.Id, "studio-login:123456");
    }

    /// <summary>
    /// A browser the user remembered goes straight in, without a code, and its device gets a new secret.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Login_InARememberedBrowser_SkipsTheCodeAndRotatesTheDevice()
    {
        var device = StudioTrustedDevice.Remember(_user.Id, "hash-of-old-device", IdentityFixture.Now.AddDays(-3));
        _fixture.StudioDevices.GetByTokenHashAsync("hash-of-old-device", Arg.Any<CancellationToken>()).Returns(device);
        _fixture.StudioDevices.RotateAsync(device.Id, "hash-of-old-device", "new-hash", IdentityFixture.Now, IdentityFixture.Now.AddDays(30), Arg.Any<CancellationToken>()).Returns(true);

        var result = await Login().HandleAsync(new StudioLoginCommand("ana", "right password", "old-device"), TestContext.Current.CancellationToken);

        var signedIn = result.ShouldBeOfType<StudioSignedIn>();
        signedIn.Session.Token.ShouldBe("new-secret");
        signedIn.Session.User.Username.ShouldBe("ana");
        signedIn.Device.Token.ShouldBe("new-secret");
        signedIn.Device.ExpiresAt.ShouldBe(IdentityFixture.Now.AddDays(30));
        await _fixture.StudioSessions.Received(1).AddAsync(Arg.Is<StudioSession>(session => session.UserId == _user.Id && session.TokenHash == "new-hash"), Arg.Any<CancellationToken>());
        await _fixture.Email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A remembered browser still needs the password: with a wrong one nothing is skipped and nothing is rotated.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Login_WithAWrongPassword_IsRefusedEvenInARememberedBrowser()
    {
        _fixture.Users.RegisterFailedLoginAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(1);

        await Should.ThrowAsync<AuthenticationFailedException>(() =>
            Login().HandleAsync(new StudioLoginCommand("ana", "wrong", "old-device"), TestContext.Current.CancellationToken));

        await _fixture.StudioDevices.DidNotReceive().RotateAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _fixture.Users.Received(1).RegisterFailedLoginAsync(_user.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A device of another user, an expired one, an unknown one, or one that someone rotated first do not skip the code.
    /// </summary>
    /// <param name="scenario">What is wrong with the device.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("other-user")]
    [InlineData("expired")]
    [InlineData("unknown")]
    [InlineData("rotated-first")]
    public async Task Login_WithADeviceThatDoesNotCount_AsksForTheCode(string scenario)
    {
        var stored = scenario switch
        {
            "other-user" => StudioTrustedDevice.Remember(Guid.CreateVersion7(), "hash-of-old-device", IdentityFixture.Now.AddDays(-3)),
            "expired" => StudioTrustedDevice.Remember(_user.Id, "hash-of-old-device", IdentityFixture.Now.AddDays(-31)),
            "rotated-first" => StudioTrustedDevice.Remember(_user.Id, "hash-of-old-device", IdentityFixture.Now.AddDays(-3)),
            _ => null,
        };
        _fixture.StudioDevices.GetByTokenHashAsync("hash-of-old-device", Arg.Any<CancellationToken>()).Returns(stored);
        _fixture.StudioDevices.RotateAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await Login().HandleAsync(new StudioLoginCommand("ana", "right password", "old-device"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<StudioCodeRequired>();
        await _fixture.StudioSessions.DidNotReceive().AddAsync(Arg.Any<StudioSession>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The web area is closed to an account without a verified email, because the second factor goes to it; no code is sent.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Login_WithoutAVerifiedEmail_IsDenied()
    {
        var unverified = Domain.Identity.User.Restore(Guid.CreateVersion7(), 1, "ana", "ana@example.com", "stored-hash", "Ana Pérez", "+50670189220", IdentityFixture.Now.AddDays(-30), 0, null);
        _fixture.Users.GetByUsernameAsync("ana", Arg.Any<CancellationToken>()).Returns(unverified);

        var error = await Should.ThrowAsync<StudioAccessDeniedException>(() =>
            Login().HandleAsync(new StudioLoginCommand("ana", "right password", null), TestContext.Current.CancellationToken));

        error.Code.ShouldBe(ApplicationErrorCodes.StudioEmailRequired);
        await _fixture.Email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A code that was sent less than a minute ago is not sent again, even with the right password.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Login_ASecondTimeWithinAMinute_IsRateLimited()
    {
        var recent = StudioLoginChallenge.Issue(_user.Id, "old-token-hash", "old", null, IdentityFixture.Now.AddSeconds(-20));
        _fixture.StudioChallenges.GetByUserIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(recent);

        var error = await Should.ThrowAsync<OtpRateLimitedException>(() =>
            Login().HandleAsync(new StudioLoginCommand("ana", "right password", null), TestContext.Current.CancellationToken));

        error.Code.ShouldBe(ApplicationErrorCodes.LoginCodeRateLimited);
        error.RetryAfter.ShouldBe(TimeSpan.FromSeconds(40));
        await _fixture.Email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The right code starts the session, spends the challenge, and does not remember the browser unless it is asked to.
    /// </summary>
    /// <param name="remember">Whether the user asked to remember the browser.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Verify_WithTheRightCode_StartsTheSession(bool remember)
    {
        var challenge = Challenge();
        _fixture.StudioChallenges.RegisterAttemptAsync("hash-of-secret", Arg.Any<CancellationToken>()).Returns(challenge with { FailedAttempts = 1 });

        var result = await Verify().HandleAsync(new StudioVerifyCodeCommand("secret", " 123456 ", remember), TestContext.Current.CancellationToken);

        result.Session.User.PhotographerId.ShouldBe(_user.Id);
        (result.Device is not null).ShouldBe(remember);
        await _fixture.StudioChallenges.Received(1).DeleteAsync(_user.Id, Arg.Any<CancellationToken>());
        await _fixture.StudioSessions.Received(1).AddAsync(Arg.Any<StudioSession>(), Arg.Any<CancellationToken>());
        await _fixture.StudioDevices.Received(remember ? 1 : 0).AddAsync(Arg.Is<StudioTrustedDevice>(device => device.UserId == _user.Id), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A wrong code is refused with the one answer every bad code gets, and starts nothing.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Verify_WithAWrongCode_IsRefused()
    {
        _fixture.StudioChallenges.RegisterAttemptAsync("hash-of-secret", Arg.Any<CancellationToken>()).Returns(Challenge() with { FailedAttempts = 1 });

        var error = await Should.ThrowAsync<DomainException>(() =>
            Verify().HandleAsync(new StudioVerifyCodeCommand("secret", "000000", true), TestContext.Current.CancellationToken));

        error.Code.ShouldBe(DomainErrorCodes.InvalidLoginCode);
        await _fixture.StudioSessions.DidNotReceive().AddAsync(Arg.Any<StudioSession>(), Arg.Any<CancellationToken>());
        await _fixture.StudioChallenges.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// After five wrong guesses the code is spent: even the right one is refused, and the challenge is removed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Verify_AfterFiveWrongGuesses_SpendsTheCode()
    {
        _fixture.StudioChallenges.RegisterAttemptAsync("hash-of-secret", Arg.Any<CancellationToken>()).Returns(Challenge() with { FailedAttempts = 6 });

        await Should.ThrowAsync<DomainException>(() =>
            Verify().HandleAsync(new StudioVerifyCodeCommand("secret", "123456", false), TestContext.Current.CancellationToken));

        await _fixture.StudioChallenges.Received(1).DeleteAsync(_user.Id, Arg.Any<CancellationToken>());
        await _fixture.StudioSessions.DidNotReceive().AddAsync(Arg.Any<StudioSession>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An expired code is refused and the challenge removed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Verify_AnExpiredCode_IsRefused()
    {
        var old = StudioLoginChallenge.Issue(_user.Id, "hash-of-secret", ExpectedCodeHash, null, IdentityFixture.Now.AddMinutes(-11)) with { FailedAttempts = 1 };
        _fixture.StudioChallenges.RegisterAttemptAsync("hash-of-secret", Arg.Any<CancellationToken>()).Returns(old);

        await Should.ThrowAsync<DomainException>(() =>
            Verify().HandleAsync(new StudioVerifyCodeCommand("secret", "123456", false), TestContext.Current.CancellationToken));

        await _fixture.StudioChallenges.Received(1).DeleteAsync(_user.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A browser without the challenge cookie, an unknown challenge and a code that is not six digits are refused; the last two without
    /// even counting a guess.
    /// </summary>
    /// <param name="token">The challenge secret the browser sent.</param>
    /// <param name="code">The code typed.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData(null, "123456")]
    [InlineData("", "123456")]
    [InlineData("secret", "12345")]
    [InlineData("secret", "1234567")]
    [InlineData("secret", "12345a")]
    [InlineData("secret", null)]
    [InlineData("unknown", "123456")]
    public async Task Verify_WithoutAUsableChallengeOrCode_IsRefused(string? token, string? code)
    {
        _fixture.StudioChallenges.RegisterAttemptAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((StudioLoginChallenge?)null);

        await Should.ThrowAsync<DomainException>(() =>
            Verify().HandleAsync(new StudioVerifyCodeCommand(token, code, false), TestContext.Current.CancellationToken));

        await _fixture.StudioSessions.DidNotReceive().AddAsync(Arg.Any<StudioSession>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A code works once: if another request spent it first, the right code is refused.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Verify_ACodeSpentByAnotherRequest_IsRefused()
    {
        _fixture.StudioChallenges.RegisterAttemptAsync("hash-of-secret", Arg.Any<CancellationToken>()).Returns(Challenge() with { FailedAttempts = 1 });
        _fixture.StudioChallenges.DeleteAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(false);

        await Should.ThrowAsync<DomainException>(() =>
            Verify().HandleAsync(new StudioVerifyCodeCommand("secret", "123456", false), TestContext.Current.CancellationToken));

        await _fixture.StudioSessions.DidNotReceive().AddAsync(Arg.Any<StudioSession>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// If the account was locked or lost its verified email between the two steps, the right code does not get in.
    /// </summary>
    /// <param name="locked">Whether the account is locked, as opposed to unverified.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Verify_ForAnAccountThatChangedMeanwhile_IsRefused(bool locked)
    {
        var changed = locked
            ? Domain.Identity.User.Restore(_user.Id, 1, "ana", "ana@example.com", "stored-hash", "Ana", "+506", IdentityFixture.Now.AddDays(-30), 5, IdentityFixture.Now.AddMinutes(10), false, IdentityFixture.Now.AddDays(-29))
            : Domain.Identity.User.Restore(_user.Id, 1, "ana", "ana@example.com", "stored-hash", "Ana", "+506", IdentityFixture.Now.AddDays(-30), 0, null);
        _fixture.Users.GetByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(changed);
        _fixture.StudioChallenges.RegisterAttemptAsync("hash-of-secret", Arg.Any<CancellationToken>()).Returns(Challenge() with { FailedAttempts = 1 });

        await Should.ThrowAsync<DomainException>(() =>
            Verify().HandleAsync(new StudioVerifyCodeCommand("secret", "123456", false), TestContext.Current.CancellationToken));

        await _fixture.StudioSessions.DidNotReceive().AddAsync(Arg.Any<StudioSession>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A new code goes to the same challenge, with the count of the window, once the minute has passed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Resend_AfterTheCooldown_SendsANewCodeToTheSameChallenge()
    {
        var current = StudioLoginChallenge.Issue(_user.Id, "hash-of-secret", "old", null, IdentityFixture.Now.AddMinutes(-3));
        _fixture.StudioChallenges.GetByTokenHashAsync("hash-of-secret", Arg.Any<CancellationToken>()).Returns(current);

        var sent = await Resend().HandleAsync(new StudioResendCodeCommand("secret"), TestContext.Current.CancellationToken);

        sent.ExpiresAt.ShouldBe(IdentityFixture.Now.AddMinutes(10));
        await _fixture.StudioChallenges.Received(1).SaveAsync(
            Arg.Is<StudioLoginChallenge>(challenge => challenge.TokenHash == "hash-of-secret" && challenge.SendCount == 2 && challenge.CodeHash == ExpectedCodeHash),
            Arg.Any<CancellationToken>());
        await _fixture.Email.Received(1).SendAsync(Arg.Is<EmailMessage>(message => message.To == "ana@example.com"), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A new code cannot be asked for within a minute.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Resend_WithinAMinute_IsRateLimited()
    {
        var current = StudioLoginChallenge.Issue(_user.Id, "hash-of-secret", "old", null, IdentityFixture.Now.AddSeconds(-10));
        _fixture.StudioChallenges.GetByTokenHashAsync("hash-of-secret", Arg.Any<CancellationToken>()).Returns(current);

        var error = await Should.ThrowAsync<OtpRateLimitedException>(() =>
            Resend().HandleAsync(new StudioResendCodeCommand("secret"), TestContext.Current.CancellationToken));

        error.Code.ShouldBe(ApplicationErrorCodes.LoginCodeRateLimited);
        await _fixture.Email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The password step is valid only for the hour of the sending window: asking for codes cannot keep an old sign-in alive. No
    /// cookie, or an unknown one, is refused too.
    /// </summary>
    /// <param name="scenario">What is wrong with the challenge.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("too-old")]
    [InlineData("unknown")]
    [InlineData("no-cookie")]
    public async Task Resend_WithoutAChallengeThatCanContinue_IsRefused(string scenario)
    {
        if (scenario == "too-old")
        {
            _fixture.StudioChallenges.GetByTokenHashAsync("hash-of-secret", Arg.Any<CancellationToken>())
                .Returns(StudioLoginChallenge.Issue(_user.Id, "hash-of-secret", "old", null, IdentityFixture.Now.AddMinutes(-61)));
        }

        await Should.ThrowAsync<DomainException>(() =>
            Resend().HandleAsync(new StudioResendCodeCommand(scenario == "no-cookie" ? null : "secret"), TestContext.Current.CancellationToken));

        await _fixture.Email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Signing out closes the session on the server, and forgets the browser only when asked.
    /// </summary>
    /// <param name="forget">Whether to forget the browser.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Logout_ClosesTheSession_AndForgetsTheBrowserOnlyWhenAsked(bool forget)
    {
        var session = StudioSession.Start(_user.Id, "hash-of-session", IdentityFixture.Now);
        var device = StudioTrustedDevice.Remember(_user.Id, "hash-of-device", IdentityFixture.Now);
        _fixture.StudioSessions.GetByTokenHashAsync("hash-of-session", Arg.Any<CancellationToken>()).Returns(session);
        _fixture.StudioDevices.GetByTokenHashAsync("hash-of-device", Arg.Any<CancellationToken>()).Returns(device);

        await Logout().HandleAsync(new StudioLogoutCommand("session", "device", forget), TestContext.Current.CancellationToken);

        await _fixture.StudioSessions.Received(1).DeleteAsync(session.Id, Arg.Any<CancellationToken>());
        await _fixture.StudioDevices.Received(forget ? 1 : 0).DeleteAsync(device.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Signing out never fails: with no cookies, or cookies that stand for nothing, it just does nothing.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Logout_WithNothingToClose_DoesNotFail()
    {
        (await Logout().HandleAsync(new StudioLogoutCommand(null, null, true), TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await Logout().HandleAsync(new StudioLogoutCommand("unknown", "unknown", true), TestContext.Current.CancellationToken)).ShouldBeTrue();

        await _fixture.StudioSessions.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A live session identifies its user, and using it again within a minute does not write to the database.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Validate_ALiveSession_IdentifiesTheUser_AndTouchesItOnlyEveryMinute()
    {
        var fresh = StudioSession.Start(_user.Id, "hash-of-session", IdentityFixture.Now.AddSeconds(-30));
        var older = StudioSession.Start(_user.Id, "hash-of-older", IdentityFixture.Now.AddMinutes(-5));
        _fixture.StudioSessions.GetByTokenHashAsync("hash-of-session", Arg.Any<CancellationToken>()).Returns(fresh);
        _fixture.StudioSessions.GetByTokenHashAsync("hash-of-older", Arg.Any<CancellationToken>()).Returns(older);

        var summary = await Validator().ValidateAsync("session", TestContext.Current.CancellationToken);
        await Validator().ValidateAsync("older", TestContext.Current.CancellationToken);

        summary.ShouldNotBeNull();
        summary.Username.ShouldBe("ana");
        summary.MaskedEmail.ShouldNotBe("ana@example.com");
        await _fixture.StudioSessions.DidNotReceive().TouchAsync(fresh.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _fixture.StudioSessions.Received(1).TouchAsync(older.Id, IdentityFixture.Now, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// No cookie, an unknown one, an idle session and a session past its fixed end are not sessions; the last two are removed.
    /// </summary>
    /// <param name="scenario">What is wrong with the session.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("no-cookie")]
    [InlineData("unknown")]
    [InlineData("idle")]
    [InlineData("past-its-end")]
    public async Task Validate_ASessionThatDoesNotCount_IsNull(string scenario)
    {
        var session = scenario switch
        {
            "idle" => StudioSession.Start(_user.Id, "hash-of-session", IdentityFixture.Now.AddHours(-3)),
            "past-its-end" => StudioSession.Start(_user.Id, "hash-of-session", IdentityFixture.Now.AddHours(-13)) with { LastSeenAt = IdentityFixture.Now.AddMinutes(-1) },
            _ => null,
        };
        _fixture.StudioSessions.GetByTokenHashAsync("hash-of-session", Arg.Any<CancellationToken>()).Returns(session);

        var summary = await Validator().ValidateAsync(scenario == "no-cookie" ? null : "session", TestContext.Current.CancellationToken);

        summary.ShouldBeNull();
        await _fixture.StudioSessions.Received(session is null ? 0 : 1).DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A session of an account that was locked, lost its verified email or no longer exists is closed on the spot.
    /// </summary>
    /// <param name="scenario">What happened to the account.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("locked")]
    [InlineData("unverified")]
    [InlineData("deleted")]
    public async Task Validate_ASessionOfAnAccountThatChanged_IsClosed(string scenario)
    {
        var session = StudioSession.Start(_user.Id, "hash-of-session", IdentityFixture.Now.AddMinutes(-5));
        _fixture.StudioSessions.GetByTokenHashAsync("hash-of-session", Arg.Any<CancellationToken>()).Returns(session);
        _fixture.Users.GetByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(scenario switch
        {
            "locked" => Domain.Identity.User.Restore(_user.Id, 1, "ana", "ana@example.com", "stored-hash", "Ana", "+506", IdentityFixture.Now.AddDays(-30), 5, IdentityFixture.Now.AddMinutes(10), false, IdentityFixture.Now.AddDays(-29)),
            "unverified" => Domain.Identity.User.Restore(_user.Id, 1, "ana", "ana@example.com", "stored-hash", "Ana", "+506", IdentityFixture.Now.AddDays(-30), 0, null),
            _ => null,
        });

        (await Validator().ValidateAsync("session", TestContext.Current.CancellationToken)).ShouldBeNull();

        await _fixture.StudioSessions.Received(1).DeleteAsync(session.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Builds the handler of the password step.
    /// </summary>
    /// <returns>The handler.</returns>
    private StudioLoginHandler Login() => new(
        _fixture.Credentials, _fixture.StudioDevices, _fixture.StudioChallenges, _fixture.StudioIssuer, _fixture.TokenGenerator, _fixture.CodeService, _fixture.Email, _fixture.Clock);

    /// <summary>
    /// Builds the handler of the code step.
    /// </summary>
    /// <returns>The handler.</returns>
    private StudioVerifyCodeHandler Verify() => new(
        _fixture.Users, _fixture.StudioChallenges, _fixture.StudioIssuer, _fixture.TokenGenerator, _fixture.CodeService, _fixture.Clock);

    /// <summary>
    /// Builds the handler that sends a new code.
    /// </summary>
    /// <returns>The handler.</returns>
    private StudioResendCodeHandler Resend() => new(
        _fixture.Users, _fixture.StudioChallenges, _fixture.TokenGenerator, _fixture.CodeService, _fixture.Email, _fixture.Clock);

    /// <summary>
    /// Builds the handler that signs out.
    /// </summary>
    /// <returns>The handler.</returns>
    private StudioLogoutHandler Logout() => new(_fixture.StudioSessions, _fixture.StudioDevices, _fixture.TokenGenerator);

    /// <summary>
    /// Builds the validator of the session cookie.
    /// </summary>
    /// <returns>The validator.</returns>
    private StudioSessionValidator Validator() => new(_fixture.StudioSessions, _fixture.Users, _fixture.TokenGenerator, _fixture.Clock);

    /// <summary>
    /// Builds a challenge for the user, with the code 123456 and the secret "secret", sent a minute ago.
    /// </summary>
    /// <returns>The challenge.</returns>
    private StudioLoginChallenge Challenge() =>
        StudioLoginChallenge.Issue(_user.Id, "hash-of-secret", ExpectedCodeHash, null, IdentityFixture.Now.AddMinutes(-1));
}
