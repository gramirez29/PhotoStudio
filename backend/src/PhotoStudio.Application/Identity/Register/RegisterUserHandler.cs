using Microsoft.Extensions.Logging;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity.Register;

/// <summary>
/// Creates a user account and signs it in, so the person who just registered does not have to type the same data again.
/// The new user gets its own identifier, which is the tenant of everything it creates afterwards. Uniqueness of the
/// username and of the email is enforced by unique indexes, not by looking first, so two simultaneous registrations cannot
/// both win. When verification is required it also sends the code that proves the email; the account is created even if that
/// email fails, and the user can ask for another code from the app.
/// </summary>
/// <param name="users">User repository.</param>
/// <param name="refreshTokens">Refresh token repository.</param>
/// <param name="passwordHasher">Password hashing.</param>
/// <param name="sessions">Builds the session tokens.</param>
/// <param name="settings">Whether sign-up is open.</param>
/// <param name="verification">Whether accounts must verify their email.</param>
/// <param name="verificationSender">Creates and sends the code that verifies the email.</param>
/// <param name="timeProvider">Clock abstraction.</param>
/// <param name="logger">Logger.</param>
public sealed partial class RegisterUserHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    SessionIssuer sessions,
    RegistrationSettings settings,
    EmailVerificationSettings verification,
    EmailVerificationSender verificationSender,
    TimeProvider timeProvider,
    ILogger<RegisterUserHandler> logger) : ICommandHandler<RegisterUserCommand, AuthSessionResponse>
{
    /// <summary>
    /// Creates the user and starts its first session.
    /// </summary>
    /// <param name="command">Data of the new user.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The new session.</returns>
    /// <exception cref="RegistrationDisabledException">When sign-up is turned off.</exception>
    /// <exception cref="DomainException">When the username, email, name, phone or password is not valid.</exception>
    /// <exception cref="ConflictException">When the username or the email is already taken.</exception>
    public async Task<AuthSessionResponse> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!settings.Enabled)
        {
            throw new RegistrationDisabledException();
        }

        // Validate everything before spending time hashing, so invalid input is cheap to refuse.
        User.ValidatePassword(command.Password);
        var username = User.NormalizeUsername(command.Username);
        var email = User.NormalizeEmail(command.Email);
        var name = User.NormalizeName(command.Name);
        var phone = User.NormalizePhone(command.Phone);

        var now = timeProvider.GetUtcNow();
        var user = User.Create(Guid.CreateVersion7(), username, email, passwordHasher.Hash(command.Password), name, phone, now);

        await users.AddAsync(user, cancellationToken);

        if (verification.Required)
        {
            await SendVerificationCodeAsync(user, now, cancellationToken);
        }

        var session = sessions.Issue(user, Guid.CreateVersion7(), now);
        await refreshTokens.AddAsync(session.RefreshToken, cancellationToken);
        return session.Response;
    }

    /// <summary>
    /// Sends the code that verifies the email of the new account. A failure is logged and swallowed: the account exists, and the
    /// user asks for another code from the app instead of losing the registration.
    /// </summary>
    /// <param name="user">The new user.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the code was sent or the failure was logged.</returns>
    private async Task SendVerificationCodeAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            await verificationSender.SendAsync(user, now, cancellationToken);
        }
        catch (EmailDeliveryException exception)
        {
            LogVerificationEmailFailed(logger, exception, user.Id);
        }
    }

    /// <summary>
    /// Logs that the code to verify the email of a new account could not be sent.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="exception">Why the email failed.</param>
    /// <param name="userId">The new user.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The code to verify the email of the new user {UserId} could not be sent.")]
    private static partial void LogVerificationEmailFailed(ILogger logger, Exception exception, Guid userId);
}
