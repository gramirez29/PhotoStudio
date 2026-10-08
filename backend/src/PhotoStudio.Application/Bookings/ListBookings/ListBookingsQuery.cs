namespace PhotoStudio.Application.Bookings.ListBookings;

/// <summary>
/// Query to list the bookings of one photographer that still matter: upcoming sessions and the recently finished ones.
/// </summary>
/// <param name="PhotographerId">Photographer (tenant) identifier.</param>
public sealed record ListBookingsQuery(Guid PhotographerId);
