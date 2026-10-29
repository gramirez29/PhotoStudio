using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Portal;

namespace PhotoStudio.Application.Portal;

/// <summary>
/// Lookups and mappings shared by the portal use cases.
/// </summary>
internal static class PortalLinkExtensions
{
    /// <summary>
    /// Finds the link a token opens. A token that matches nothing, was revoked or expired is reported exactly the same way,
    /// so the response never tells an attacker which of the three it was.
    /// </summary>
    /// <param name="links">Link repository.</param>
    /// <param name="tokens">Hashes the token taken from the URL.</param>
    /// <param name="token">Secret token from the URL.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The active link.</returns>
    /// <exception cref="NotFoundException">When the token does not open a booking.</exception>
    public static async Task<BookingLink> ResolveAsync(
        this IBookingLinkRepository links,
        IRefreshTokenGenerator tokens,
        string token,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new NotFoundException(nameof(Booking), Guid.Empty);
        }

        var link = await links.GetByTokenHashAsync(tokens.Hash(token), cancellationToken);
        if (link is null || !link.IsActiveAt(now))
        {
            throw new NotFoundException(nameof(Booking), Guid.Empty);
        }

        return link;
    }

    /// <summary>
    /// Loads the booking of a link.
    /// </summary>
    /// <param name="repository">Booking repository.</param>
    /// <param name="link">Active link.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The booking.</returns>
    /// <exception cref="NotFoundException">When the booking no longer exists or does not belong to the photographer recorded in the link.</exception>
    public static async Task<Booking> GetBookingAsync(this IBookingRepository repository, BookingLink link, CancellationToken cancellationToken)
    {
        var booking = await repository.GetByIdAsync(link.BookingId, cancellationToken);
        if (booking is null || booking.PhotographerId != link.PhotographerId)
        {
            throw new NotFoundException(nameof(Booking), Guid.Empty);
        }

        return booking;
    }

    /// <summary>
    /// Maps a booking to what its client sees in the portal.
    /// </summary>
    /// <param name="booking">Booking to map.</param>
    /// <returns>The portal view.</returns>
    public static PortalBookingResponse ToPortalResponse(this Booking booking)
    {
        ArgumentNullException.ThrowIfNull(booking);

        var canPay = booking.Status is BookingStatus.Tentative or BookingStatus.Confirmed or BookingStatus.Completed
            && booking.Balance.Amount > 0;

        return new PortalBookingResponse(
            StudioBrand.Name,
            booking.Client.Name,
            booking.PackageName,
            booking.Status,
            booking.Slot.Start,
            booking.Slot.End,
            booking.ExpiresAt,
            booking.PackagePrice.ToResponse(),
            booking.DepositRequired.ToResponse(),
            booking.TotalVerifiedPaid.ToResponse(),
            booking.Balance.ToResponse(),
            booking.Contract is not null,
            [.. booking.Payments.Select(payment => new PortalPaymentResponse(payment.Amount.ToResponse(), payment.Status, payment.RecordedAt))],
            canPay,
            booking.Client.Email is not null,
            booking.Status == BookingStatus.Tentative && booking.Contract is null && booking.Client.Email is not null);
    }

    /// <summary>
    /// Hides most of an email address, keeping enough for its owner to recognize it, for example <c>m***@gmail.com</c>.
    /// </summary>
    /// <param name="email">Normalized email address.</param>
    /// <returns>The masked address.</returns>
    public static string MaskEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }
}
