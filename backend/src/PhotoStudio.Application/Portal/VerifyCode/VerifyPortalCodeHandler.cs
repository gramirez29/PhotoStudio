using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Portal;

namespace PhotoStudio.Application.Portal.VerifyCode;

/// <summary>
/// Checks the code the client typed and, when it is right, opens a portal session for that booking. A code works once, expires
/// after <see cref="PortalOtp.Lifetime"/> and tolerates <see cref="PortalOtp.MaxFailedAttempts"/> wrong guesses. Every guess is
/// counted atomically before it is compared. A wrong, expired or spent code gets the same answer.
/// </summary>
/// <param name="bookings">Booking repository.</param>
/// <param name="links">Link repository.</param>
/// <param name="codes">Verification code repository.</param>
/// <param name="codeService">Hashes the codes.</param>
/// <param name="tokens">Hashes the token taken from the URL.</param>
/// <param name="sessions">Issues the portal session token.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class VerifyPortalCodeHandler(
    IBookingRepository bookings,
    IBookingLinkRepository links,
    IPortalOtpRepository codes,
    IOtpCodeService codeService,
    IRefreshTokenGenerator tokens,
    IPortalSessionIssuer sessions,
    TimeProvider timeProvider) : ICommandHandler<VerifyPortalCodeCommand, PortalSessionResponse>
{
    /// <summary>
    /// Verifies the code.
    /// </summary>
    /// <param name="command">Token of the link and the typed code.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The portal session.</returns>
    /// <exception cref="NotFoundException">When the token does not open a booking.</exception>
    /// <exception cref="DomainException">When the code is wrong, expired or spent.</exception>
    public async Task<PortalSessionResponse> HandleAsync(VerifyPortalCodeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var link = await links.ResolveAsync(tokens, command.Token, now, cancellationToken);
        var booking = await bookings.GetBookingAsync(link, cancellationToken);

        var typed = (command.Code ?? string.Empty).Trim();
        if (typed.Length != PortalOtp.CodeLength || !typed.All(char.IsAsciiDigit))
        {
            throw InvalidCode();
        }

        var stored = await codes.RegisterAttemptAsync(booking.Id, cancellationToken);
        if (stored is null)
        {
            throw InvalidCode();
        }

        if (stored.IsExpiredAt(now) || stored.FailedAttempts > PortalOtp.MaxFailedAttempts)
        {
            await codes.DeleteAsync(booking.Id, cancellationToken);
            throw InvalidCode();
        }

        if (!codeService.Matches(stored.CodeHash, codeService.Hash(booking.Id, typed)))
        {
            if (stored.FailedAttempts >= PortalOtp.MaxFailedAttempts)
            {
                await codes.DeleteAsync(booking.Id, cancellationToken);
            }

            throw InvalidCode();
        }

        // A code is single-use: it is gone as soon as it opens a session.
        await codes.DeleteAsync(booking.Id, cancellationToken);
        var session = sessions.Issue(booking.Id, now);
        return new PortalSessionResponse(session.Value, session.ExpiresAt);
    }

    /// <summary>
    /// Builds the generic failure used for every rejected code.
    /// </summary>
    /// <returns>The exception to throw.</returns>
    private static DomainException InvalidCode() =>
        new(ApplicationErrorCodes.InvalidCode, "The code is not correct or has expired. Ask for a new one.");
}
