namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Port that issues the short-lived token a client gets after verifying a code. The token opens exactly one booking and
/// is rejected by every photographer endpoint. Implemented in the Infrastructure layer.
/// </summary>
public interface IPortalSessionIssuer
{
    /// <summary>
    /// Issues a portal session token.
    /// </summary>
    /// <param name="bookingId">Booking the token opens; becomes a claim of the token.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The token and its expiry.</returns>
    AccessToken Issue(Guid bookingId, DateTimeOffset now);
}
