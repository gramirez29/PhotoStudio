namespace PhotoStudio.Application.Bookings.ExpireTentativeBookings;

/// <summary>
/// Command, issued by a background job, to expire the tentative bookings whose hold has ended (B7).
/// </summary>
/// <param name="BatchSize">Maximum number of bookings to examine in one run.</param>
public sealed record ExpireTentativeBookingsCommand(int BatchSize);

/// <summary>
/// Outcome of one expiration run.
/// </summary>
/// <param name="Expired">Bookings that moved to <c>Expired</c>.</param>
/// <param name="Skipped">Bookings left untouched because a guard still failed (for example a proof of payment waiting for review) or another write won the race.</param>
public sealed record ExpireTentativeBookingsResult(int Expired, int Skipped);
