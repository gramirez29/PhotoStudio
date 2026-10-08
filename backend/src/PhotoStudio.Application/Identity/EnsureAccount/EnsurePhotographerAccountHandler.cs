using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity.EnsureAccount;

/// <summary>
/// Creates the photographer account, or brings an existing one in line with the configured password. While the account is
/// managed from the environment, the environment is the source of truth for the password: changing the variable and
/// restarting resets it, which is also the recovery path when it is forgotten. Replacing the password revokes every
/// session, so a leaked session cannot outlive a reset.
/// </summary>
/// <param name="accounts">Account repository.</param>
/// <param name="refreshTokens">Refresh token repository.</param>
/// <param name="passwordHasher">Password hashing.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class EnsurePhotographerAccountHandler(
    IPhotographerAccountRepository accounts,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider) : ICommandHandler<EnsurePhotographerAccountCommand, EnsureAccountResult>
{
    /// <summary>
    /// Minimum length of a password set from the environment.
    /// </summary>
    public const int MinimumPasswordLength = 10;

    /// <summary>
    /// Creates or updates the account.
    /// </summary>
    /// <param name="command">Account data.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>What was done.</returns>
    /// <exception cref="DomainException">When the email is not valid or the password is too short.</exception>
    public async Task<EnsureAccountResult> HandleAsync(EnsurePhotographerAccountCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrEmpty(command.Password) || command.Password.Length < MinimumPasswordLength)
        {
            throw new DomainException(
                DomainErrorCodes.WeakPassword,
                $"The password must have at least {MinimumPasswordLength} characters.");
        }

        var email = PhotographerAccount.NormalizeEmail(command.Email);
        var now = timeProvider.GetUtcNow();
        var existing = await accounts.GetByEmailAsync(email, cancellationToken);

        if (existing is null)
        {
            var account = PhotographerAccount.Create(
                command.PhotographerId ?? Guid.CreateVersion7(),
                email,
                passwordHasher.Hash(command.Password),
                now);
            await accounts.AddAsync(account, cancellationToken);
            return new EnsureAccountResult(account.Id, EnsureAccountOutcome.Created);
        }

        if (passwordHasher.Verify(existing.PasswordHash, command.Password))
        {
            return new EnsureAccountResult(existing.Id, EnsureAccountOutcome.Unchanged);
        }

        await accounts.SetPasswordHashAsync(existing.Id, passwordHasher.Hash(command.Password), cancellationToken);
        await refreshTokens.RevokeAllAsync(existing.Id, now, cancellationToken);
        return new EnsureAccountResult(existing.Id, EnsureAccountOutcome.PasswordUpdated);
    }
}
