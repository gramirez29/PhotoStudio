using PhotoStudio.Application.Identity.EnsureAccount;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.UnitTests.Identity;

/// <summary>
/// Tests of <see cref="EnsurePhotographerAccountHandler"/>.
/// </summary>
public sealed class EnsurePhotographerAccountHandlerTests
{
    private const string Password = "a-long-enough-password";

    private readonly IdentityFixture _fixture = new();

    /// <summary>
    /// Without an account for the email, one is created with the given identifier and the hash of the password.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithoutAnAccount_CreatesItKeepingTheGivenIdentifier()
    {
        var photographerId = Guid.CreateVersion7();
        _fixture.Accounts.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((PhotographerAccount?)null);
        _fixture.PasswordHasher.Hash(Password).Returns("the-hash");

        var result = await NewHandler().HandleAsync(
            new EnsurePhotographerAccountCommand(photographerId, " Ana@Example.com ", Password),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new EnsureAccountResult(photographerId, EnsureAccountOutcome.Created));
        await _fixture.Accounts.Received(1).AddAsync(
            Arg.Is<PhotographerAccount>(account => account.Id == photographerId && account.Email == "ana@example.com" && account.PasswordHash == "the-hash"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Without a given identifier, a new one is generated.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithoutAnIdentifier_GeneratesOne()
    {
        _fixture.Accounts.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((PhotographerAccount?)null);
        _fixture.PasswordHasher.Hash(Password).Returns("the-hash");

        var result = await NewHandler().HandleAsync(
            new EnsurePhotographerAccountCommand(null, "ana@example.com", Password),
            TestContext.Current.CancellationToken);

        result.PhotographerId.ShouldNotBe(Guid.Empty);
        result.Outcome.ShouldBe(EnsureAccountOutcome.Created);
    }

    /// <summary>
    /// An existing account with the same password is left untouched.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithTheSamePassword_ChangesNothing()
    {
        var account = IdentityFixture.Account();
        _fixture.Accounts.GetByEmailAsync(account.Email, Arg.Any<CancellationToken>()).Returns(account);
        _fixture.PasswordHasher.Verify(account.PasswordHash, Password).Returns(true);

        var result = await NewHandler().HandleAsync(
            new EnsurePhotographerAccountCommand(Guid.CreateVersion7(), account.Email, Password),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new EnsureAccountResult(account.Id, EnsureAccountOutcome.Unchanged));
        await _fixture.Accounts.DidNotReceive().SetPasswordHashAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _fixture.RefreshTokens.DidNotReceive().RevokeAllAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A different password replaces the hash and signs every device out.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAnotherPassword_ReplacesItAndRevokesEverySession()
    {
        var account = IdentityFixture.Account();
        _fixture.Accounts.GetByEmailAsync(account.Email, Arg.Any<CancellationToken>()).Returns(account);
        _fixture.PasswordHasher.Verify(account.PasswordHash, Password).Returns(false);
        _fixture.PasswordHasher.Hash(Password).Returns("the-new-hash");

        var result = await NewHandler().HandleAsync(
            new EnsurePhotographerAccountCommand(null, account.Email, Password),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new EnsureAccountResult(account.Id, EnsureAccountOutcome.PasswordUpdated));
        await _fixture.Accounts.Received(1).SetPasswordHashAsync(account.Id, "the-new-hash", Arg.Any<CancellationToken>());
        await _fixture.RefreshTokens.Received(1).RevokeAllAsync(account.Id, IdentityFixture.Now, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A password shorter than the minimum is rejected before anything is read or written.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAShortPassword_IsRejected()
    {
        var exception = await Should.ThrowAsync<DomainException>(
            () => NewHandler().HandleAsync(
                new EnsurePhotographerAccountCommand(null, "ana@example.com", new string('x', EnsurePhotographerAccountHandler.MinimumPasswordLength - 1)),
                TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.WeakPassword);
        await _fixture.Accounts.DidNotReceive().GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An email without a valid shape is rejected.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAnInvalidEmail_IsRejected()
    {
        var exception = await Should.ThrowAsync<DomainException>(
            () => NewHandler().HandleAsync(
                new EnsurePhotographerAccountCommand(null, "not-an-email", Password),
                TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidEmail);
    }

    /// <summary>
    /// Creates the handler under test.
    /// </summary>
    /// <returns>The handler.</returns>
    private EnsurePhotographerAccountHandler NewHandler() => new(
        _fixture.Accounts,
        _fixture.RefreshTokens,
        _fixture.PasswordHasher,
        _fixture.Clock);
}
