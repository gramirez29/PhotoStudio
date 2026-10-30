using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity.Studio;

/// <summary>
/// Second step of the web sign-in: the code from the email. The check is the one of the other codes of the product: the challenge
/// is found by the secret of the browser, each guess is counted atomically before it is compared, five wrong guesses spend the
/// code, a code works once, and the answer to a wrong, expired or spent code is always the same. A correct code starts the session
/// and, if the user asked, remembers the browser.
/// </summary>
/// <param name="users">User repository.</param>
/// <param name="challenges">Challenges waiting for their code.</param>
/// <param name="issuer">Starts sessions and remembers browsers.</param>
/// <param name="tokens">Hashes the secret of the challenge.</param>
/// <param name="codeService">Hashes and compares codes.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class StudioVerifyCodeHandler(
    IUserRepository users,
    IStudioChallengeRepository challenges,
    StudioSessionIssuer issuer,
    IRefreshTokenGenerator tokens,
    IOtpCodeService codeService,
    TimeProvider timeProvider) : ICommandHandler<StudioVerifyCodeCommand, StudioVerifyResult>
{
    /// <summary>
    /// Verifies the code and signs the user in.
    /// </summary>
    /// <param name="command">The challenge secret and the code typed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The session, and the remembered browser when it was asked for.</returns>
    /// <exception cref="DomainException">When the code is wrong, expired or spent, or the challenge does not belong to this browser.</exception>
    public async Task<StudioVerifyResult> HandleAsync(StudioVerifyCodeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var typed = (command.Code ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(command.ChallengeToken) || typed.Length != StudioLoginChallenge.CodeLength || !typed.All(char.IsAsciiDigit))
        {
            throw InvalidCode();
        }

        var tokenHash = tokens.Hash(command.ChallengeToken);
        var stored = await challenges.RegisterAttemptAsync(tokenHash, cancellationToken);
        if (stored is null)
        {
            throw InvalidCode();
        }

        if (stored.IsExpiredAt(now) || stored.FailedAttempts > StudioLoginChallenge.MaxFailedAttempts)
        {
            await challenges.DeleteAsync(stored.UserId, cancellationToken);
            throw InvalidCode();
        }

        if (!codeService.Matches(stored.CodeHash, StudioCodes.Hash(codeService, stored.UserId, typed)))
        {
            throw InvalidCode();
        }

        // The code works once: if another request spent it first, this one is refused.
        if (!await challenges.DeleteAsync(stored.UserId, cancellationToken))
        {
            throw InvalidCode();
        }

        var user = await users.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null || !user.IsEmailVerified || user.IsLockedAt(now))
        {
            throw InvalidCode();
        }

        var session = await issuer.StartSessionAsync(user, now, cancellationToken);
        var device = command.RememberDevice ? await issuer.RememberDeviceAsync(user, now, cancellationToken) : null;
        return new StudioVerifyResult(session, device);
    }

    /// <summary>
    /// Builds the generic failure used for every rejected code.
    /// </summary>
    /// <returns>The exception to throw.</returns>
    private static DomainException InvalidCode() => new(DomainErrorCodes.InvalidLoginCode, "The code is not valid. Ask for a new one.");
}
