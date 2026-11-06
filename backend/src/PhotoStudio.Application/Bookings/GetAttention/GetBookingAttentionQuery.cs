namespace PhotoStudio.Application.Bookings.GetAttention;

/// <summary>
/// Limits of the attention summary.
/// </summary>
public static class AttentionLimits
{
    /// <summary>
    /// Hours before the end of its hold at which a tentative booking counts as about to expire.
    /// </summary>
    public const int ExpiringSoonHours = 24;
}

/// <summary>
/// Query for what in the bookings of a photographer is waiting for them, for the home screen of the app.
/// </summary>
/// <param name="PhotographerId">Photographer (tenant) identifier.</param>
public sealed record GetBookingAttentionQuery(Guid PhotographerId);

/// <summary>
/// What in the bookings of a photographer is waiting for them.
/// </summary>
/// <param name="PendingProofs">Bookings with a proof of payment that the photographer has not verified or rejected yet.</param>
/// <param name="ExpiringSoon">Tentative bookings whose hold ends within <see cref="AttentionLimits.ExpiringSoonHours"/> hours, which will expire unless the client signs and pays. A booking with a proof waiting for review does not expire, so it is not counted here.</param>
public sealed record BookingAttentionResponse(int PendingProofs, int ExpiringSoon);
