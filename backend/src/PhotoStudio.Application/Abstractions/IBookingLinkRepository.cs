using PhotoStudio.Domain.Portal;

namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Persistence port for <see cref="BookingLink"/>. Implemented in the Infrastructure layer.
/// </summary>
public interface IBookingLinkRepository
{
    /// <summary>
    /// Finds a link by the hash of its token.
    /// </summary>
    /// <param name="tokenHash">Hash of the token taken from the URL.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The link, or <see langword="null"/> when none matches. It may be expired or revoked; the caller checks.</returns>
    Task<BookingLink?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a new link and revokes every other active link of the same booking, as one atomic operation, so a booking
    /// never has two working links.
    /// </summary>
    /// <param name="link">New link.</param>
    /// <param name="now">Current instant, stored as the revocation instant of the replaced links.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the link is stored.</returns>
    Task ReplaceActiveAsync(BookingLink link, DateTimeOffset now, CancellationToken cancellationToken);
}
