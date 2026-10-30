using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Portal;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity.Studio;

/// <summary>
/// First step of the web sign-in: the password. A browser the user remembered goes straight in; any other gets a code in the
/// email of the account, which has to come back from the same browser. The password is checked exactly like the app's, with the
/// same lockout. The web area is closed to an account without a verified email, because the second factor goes to it.
/// </summary>
/// <param name="credentials">Checks the username and the password.</param>
/// <param name="devices">Remembered browsers.</param>
/// <param name="challenges">Challenges waiting for their code.</param>
/// <param name="issuer">Starts sessions.</param>
/// <param name="tokens">Generates and hashes secrets.</param>
/// <param name="codeService">Generates and hashes codes.</param>
/// <param name="email">Sends the email.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class StudioLoginHandler(
    CredentialChecker credentials,
    IStudioDeviceRepository devices,
    IStudioChallengeRepository challenges,
    StudioSessionIssuer issuer,
    IRefreshTokenGenerator tokens,
    IOtpCodeService codeService,
    IEmailSender email,
    TimeProvider timeProvider) : ICommandHandler<StudioLoginCommand, StudioLoginResult>
{
    /// <summary>
    /// Checks the password and either signs the user in or asks for the code.
    /// </summary>
    /// <param name="command">Username, password and the device cookie, if any.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The session, or the challenge that is waiting for its code.</returns>
    /// <exception cref="AuthenticationFailedException">When the credentials are not accepted.</exception>
    /// <exception cref="AccountLockedException">When the user is locked by too many failed attempts.</exception>
    /// <exception cref="StudioAccessDeniedException">When the account has no verified email.</exception>
    /// <exception cref="OtpRateLimitedException">When a code was sent less than a minute ago or too many times in the last hour.</exception>
    /// <exception cref="EmailDeliveryException">When the email provider did not accept the message.</exception>
    public async Task<StudioLoginResult> HandleAsync(StudioLoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var user = await credentials.VerifyAsync(command.Username, command.Password, now, cancellationToken);
        if (user.Email.Length == 0 || !user.IsEmailVerified)
        {
            throw new StudioAccessDeniedException(ApplicationErrorCodes.StudioEmailRequired, "The web area needs an account with a verified email.");
        }

        if (await TryUseRememberedDeviceAsync(user, command.DeviceToken, now, cancellationToken) is { } device)
        {
            return new StudioSignedIn(await issuer.StartSessionAsync(user, now, cancellationToken), device);
        }

        var previous = await challenges.GetByUserIdAsync(user.Id, cancellationToken);
        if (previous?.RetryAfterAt(now) is { } retryAfter)
        {
            throw new OtpRateLimitedException(retryAfter, ApplicationErrorCodes.LoginCodeRateLimited);
        }

        var secret = tokens.Generate();
        var code = codeService.Generate();
        var record = StudioLoginChallenge.Issue(user.Id, secret.Hash, StudioCodes.Hash(codeService, user.Id, code), previous, now);

        // Stored first and sent after, like every code of the product: if the email fails the user asks again once the cooldown
        // passes, and a code that was stored but never delivered is harmless.
        await challenges.SaveAsync(record, cancellationToken);
        await email.SendAsync(IdentityEmails.StudioLoginCode(user.Email, user.Name, code), cancellationToken);
        return new StudioCodeRequired(secret.Secret, PortalLinkExtensions.MaskEmail(user.Email), record.ExpiresAt);
    }

    /// <summary>
    /// Accepts the remembered browser, if the cookie it sent is one of this user's that has not been forgotten, and gives it a new
    /// secret so a copied cookie works at most once.
    /// </summary>
    /// <param name="user">User who gave the right password.</param>
    /// <param name="deviceToken">Secret of the device cookie, if the browser sent one.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The device with its new secret, or <see langword="null"/> when the browser is not remembered.</returns>
    private async Task<StudioIssuedDevice?> TryUseRememberedDeviceAsync(User user, string? deviceToken, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
        {
            return null;
        }

        var device = await devices.GetByTokenHashAsync(tokens.Hash(deviceToken), cancellationToken);
        if (device is null || !device.IsTrustedFor(user.Id, now))
        {
            return null;
        }

        var next = tokens.Generate();
        var expiresAt = now + StudioTrustedDevice.Lifetime;
        return await devices.RotateAsync(device.Id, device.TokenHash, next.Hash, now, expiresAt, cancellationToken)
            ? new StudioIssuedDevice(next.Secret, expiresAt)
            : null;
    }
}
