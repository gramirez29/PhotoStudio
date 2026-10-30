using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Portal;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity.Studio;

/// <summary>
/// Sends a new code to a sign-in that is waiting for it, with the limits of every code of the product (a minute between codes, five
/// an hour). The password was checked in the first step and stays valid only for the hour of the sending window: after that the
/// user has to start again, so an old challenge cannot be kept alive by asking for codes.
/// </summary>
/// <param name="users">User repository.</param>
/// <param name="challenges">Challenges waiting for their code.</param>
/// <param name="tokens">Hashes the secret of the challenge.</param>
/// <param name="codeService">Generates and hashes codes.</param>
/// <param name="email">Sends the email.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class StudioResendCodeHandler(
    IUserRepository users,
    IStudioChallengeRepository challenges,
    IRefreshTokenGenerator tokens,
    IOtpCodeService codeService,
    IEmailSender email,
    TimeProvider timeProvider) : ICommandHandler<StudioResendCodeCommand, StudioCodeSentResponse>
{
    /// <summary>
    /// Sends a new code.
    /// </summary>
    /// <param name="command">The challenge secret of the browser.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Where the code went and when it expires.</returns>
    /// <exception cref="DomainException">When the browser has no challenge, or it is too old to be continued.</exception>
    /// <exception cref="OtpRateLimitedException">When a code was sent less than a minute ago or too many times in the last hour.</exception>
    /// <exception cref="EmailDeliveryException">When the email provider did not accept the message.</exception>
    public async Task<StudioCodeSentResponse> HandleAsync(StudioResendCodeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        if (string.IsNullOrWhiteSpace(command.ChallengeToken))
        {
            throw InvalidCode();
        }

        var current = await challenges.GetByTokenHashAsync(tokens.Hash(command.ChallengeToken), cancellationToken);
        if (current is null || now >= current.WindowStartedAt + StudioLoginChallenge.SendWindow)
        {
            throw InvalidCode();
        }

        var user = await users.GetByIdAsync(current.UserId, cancellationToken);
        if (user is null || !user.IsEmailVerified || user.IsLockedAt(now))
        {
            throw InvalidCode();
        }

        if (current.RetryAfterAt(now) is { } retryAfter)
        {
            throw new OtpRateLimitedException(retryAfter, ApplicationErrorCodes.LoginCodeRateLimited);
        }

        var code = codeService.Generate();
        var record = StudioLoginChallenge.Issue(user.Id, current.TokenHash, StudioCodes.Hash(codeService, user.Id, code), current, now);
        await challenges.SaveAsync(record, cancellationToken);
        await email.SendAsync(IdentityEmails.StudioLoginCode(user.Email, user.Name, code), cancellationToken);
        return new StudioCodeSentResponse(PortalLinkExtensions.MaskEmail(user.Email), record.ExpiresAt);
    }

    /// <summary>
    /// Builds the generic failure used when the challenge cannot be continued.
    /// </summary>
    /// <returns>The exception to throw.</returns>
    private static DomainException InvalidCode() => new(DomainErrorCodes.InvalidLoginCode, "The code is not valid. Sign in again.");
}
