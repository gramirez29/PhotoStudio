using Microsoft.Extensions.Logging;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity.RecoverPassword;

/// <summary>
/// Sends the code that lets a user who forgot their password choose a new one, to the verified email of the account. It answers the
/// same whether or not the account exists, whether the code was sent or the user has to wait, and even when the email provider fails,
/// so nobody can use it to find out which usernames or emails are registered; the limits of the code (a minute between codes, five an
/// hour) are kept silently and the endpoint is also limited per address. The code never travels in the response.
/// </summary>
/// <param name="users">User repository.</param>
/// <param name="codes">Stores the code, whose counters carry the limits.</param>
/// <param name="codeService">Generates and hashes codes.</param>
/// <param name="email">Sends the email.</param>
/// <param name="timeProvider">Clock abstraction.</param>
/// <param name="logger">Logger.</param>
public sealed partial class RequestPasswordResetHandler(
    IUserRepository users,
    IPasswordResetRepository codes,
    IOtpCodeService codeService,
    IEmailSender email,
    TimeProvider timeProvider,
    ILogger<RequestPasswordResetHandler> logger) : ICommandHandler<RequestPasswordResetCommand, PasswordResetRequestedResponse>
{
    /// <summary>
    /// Sends the code when the account can receive it.
    /// </summary>
    /// <param name="command">What the user remembers of the account.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The same answer in every case.</returns>
    public async Task<PasswordResetRequestedResponse> HandleAsync(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var user = await new AccountFinder(users).FindRecoverableAsync(command.Identifier, cancellationToken);
        if (user is null)
        {
            return new PasswordResetRequestedResponse();
        }

        var previous = await codes.GetAsync(user.Id, cancellationToken);
        if (previous?.IsSendBlockedAt(now) == true)
        {
            return new PasswordResetRequestedResponse();
        }

        var code = codeService.Generate();
        var record = PasswordResetCode.Issue(user.Id, codeService.Hash(user.Id, PasswordResetCode.HashPurpose + code), previous, now);

        // The record is stored first and the email sent after: a code that was stored but never delivered is harmless.
        await codes.SaveAsync(record, cancellationToken);

        try
        {
            await email.SendAsync(IdentityEmails.PasswordResetCode(user.Email, user.Name, code), cancellationToken);
        }
        catch (EmailDeliveryException exception)
        {
            // Not reported to the caller: a failure that only happens for real accounts would reveal them.
            LogEmailFailed(logger, exception, user.Id);
        }

        return new PasswordResetRequestedResponse();
    }

    /// <summary>
    /// Logs that the code to choose a new password could not be sent.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="exception">Why the email failed.</param>
    /// <param name="userId">The user the code was for.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The code to choose a new password for user {UserId} could not be sent.")]
    private static partial void LogEmailFailed(ILogger logger, Exception exception, Guid userId);
}
