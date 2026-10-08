using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Identity;
using PhotoStudio.Application.Identity.Register;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.UnitTests.Identity;

/// <summary>
/// Tests of <see cref="RegisterUserHandler"/>.
/// </summary>
public sealed class RegisterUserHandlerTests
{
    private const string Password = "a-long-enough-password";

    private readonly IdentityFixture _fixture = new();

    /// <summary>
    /// A valid registration stores the user with the normalized data and the hash of the password, never the password.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_StoresTheNormalizedUserWithTheHashOfThePassword()
    {
        _fixture.PasswordHasher.Hash(Password).Returns("the-hash");

        await NewHandler().HandleAsync(
            new RegisterUserCommand("  Ana.Photo ", "  Ana@Example.COM ", Password, "  Ana Pérez ", "+506 7018-9220"),
            TestContext.Current.CancellationToken);

        await _fixture.Users.Received(1).AddAsync(
            Arg.Is<User>(user =>
                user.Username == "ana.photo"
                && user.Email == "ana@example.com"
                && user.PasswordHash == "the-hash"
                && user.Name == "Ana Pérez"
                && user.Phone == "+50670189220"
                && user.CreatedAt == IdentityFixture.Now
                && user.Id != Guid.Empty),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The new user is signed in at once: the response carries a session for the identifier the user was given.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_SignsTheNewUserIn()
    {
        _fixture.PasswordHasher.Hash(Password).Returns("the-hash");
        User? stored = null;
        await _fixture.Users.AddAsync(Arg.Do<User>(user => stored = user), Arg.Any<CancellationToken>());

        var session = await NewHandler().HandleAsync(
            new RegisterUserCommand("ana", "ana@example.com", Password, "Ana Pérez", "70189220"),
            TestContext.Current.CancellationToken);

        stored.ShouldNotBeNull();
        session.PhotographerId.ShouldBe(stored.Id);
        session.Username.ShouldBe("ana");
        session.Email.ShouldBe("ana@example.com");
        session.Name.ShouldBe("Ana Pérez");
        session.AccessToken.ShouldBe("access-jwt");
        session.RefreshToken.ShouldBe("new-secret");
        await _fixture.RefreshTokens.Received(1).AddAsync(
            Arg.Is<RefreshToken>(token => token.PhotographerId == stored.Id && token.TokenHash == "new-hash"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Each registration gets its own identifier, which is the tenant of everything the user creates.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_GivesEveryUserItsOwnIdentifier()
    {
        var first = await NewHandler().HandleAsync(new RegisterUserCommand("ana", "ana@example.com", Password, "Ana", "70189220"), TestContext.Current.CancellationToken);
        var second = await NewHandler().HandleAsync(new RegisterUserCommand("luis", "luis@example.com", Password, "Luis", "70189221"), TestContext.Current.CancellationToken);

        first.PhotographerId.ShouldNotBe(second.PhotographerId);
    }

    /// <summary>
    /// A taken username is a conflict and no session is created.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithATakenUsername_ThrowsConflictAndStartsNoSession()
    {
        _fixture.Users.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ConflictException(ApplicationErrorCodes.UsernameTaken, "taken")));

        var exception = await Should.ThrowAsync<ConflictException>(
            () => NewHandler().HandleAsync(new RegisterUserCommand("ana", "ana@example.com", Password, "Ana", "70189220"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.UsernameTaken);
        await _fixture.RefreshTokens.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A taken email is a conflict with its own code and no session is created.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithATakenEmail_ThrowsConflictWithItsOwnCode()
    {
        _fixture.Users.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ConflictException(ApplicationErrorCodes.EmailTaken, "taken")));

        var exception = await Should.ThrowAsync<ConflictException>(
            () => NewHandler().HandleAsync(new RegisterUserCommand("ana", "ana@example.com", Password, "Ana", "70189220"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.EmailTaken);
        await _fixture.RefreshTokens.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A weak password is refused before any hashing work is spent on it.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAShortPassword_IsRefusedBeforeHashing()
    {
        var exception = await Should.ThrowAsync<DomainException>(
            () => NewHandler().HandleAsync(new RegisterUserCommand("ana", "ana@example.com", "short", "Ana", "70189220"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.WeakPassword);
        _fixture.PasswordHasher.DidNotReceive().Hash(Arg.Any<string>());
        await _fixture.Users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Each invalid field is refused with its own code before hashing or storing anything.
    /// </summary>
    /// <param name="username">Username to send.</param>
    /// <param name="email">Email to send.</param>
    /// <param name="name">Name to send.</param>
    /// <param name="phone">Phone to send.</param>
    /// <param name="expectedCode">Code the refusal must carry.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("a", "ana@example.com", "Ana", "70189220", DomainErrorCodes.InvalidUsername)]
    [InlineData("ana", "not-an-email", "Ana", "70189220", DomainErrorCodes.InvalidEmail)]
    [InlineData("ana", "ana@example.com", "  ", "70189220", DomainErrorCodes.InvalidName)]
    [InlineData("ana", "ana@example.com", "Ana", "12", DomainErrorCodes.InvalidPhone)]
    public async Task HandleAsync_WithAnInvalidField_IsRefusedBeforeHashing(string username, string email, string name, string phone, string expectedCode)
    {
        var exception = await Should.ThrowAsync<DomainException>(
            () => NewHandler().HandleAsync(new RegisterUserCommand(username, email, Password, name, phone), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(expectedCode);
        _fixture.PasswordHasher.DidNotReceive().Hash(Arg.Any<string>());
        await _fixture.Users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When sign-up is turned off nothing is validated, hashed or stored.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenRegistrationIsDisabled_IsRefused()
    {
        var exception = await Should.ThrowAsync<RegistrationDisabledException>(
            () => NewHandler(registrationEnabled: false).HandleAsync(
                new RegisterUserCommand("ana", "ana@example.com", Password, "Ana", "70189220"),
                TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.RegistrationDisabled);
        _fixture.PasswordHasher.DidNotReceive().Hash(Arg.Any<string>());
        await _fixture.Users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Creates the handler under test.
    /// </summary>
    /// <param name="registrationEnabled">Whether sign-up is open.</param>
    /// <returns>The handler.</returns>
    private RegisterUserHandler NewHandler(bool registrationEnabled = true) => new(
        _fixture.Users,
        _fixture.RefreshTokens,
        _fixture.PasswordHasher,
        _fixture.Sessions,
        new RegistrationSettings(registrationEnabled),
        _fixture.Clock);
}
