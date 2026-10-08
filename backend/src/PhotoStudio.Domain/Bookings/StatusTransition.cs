namespace PhotoStudio.Domain.Bookings;

/// <summary>
/// Audit entry of a booking transition. The history is the evidence used to resolve disputes.
/// </summary>
/// <param name="From">Status before the transition; <see langword="null"/> for the creation entry.</param>
/// <param name="To">Status after the transition.</param>
/// <param name="Actor">Who performed the transition.</param>
/// <param name="Channel">Channel of the transition; <see langword="null"/> for automatic transitions.</param>
/// <param name="Reason">Optional reason, mandatory for some transitions such as cancellations.</param>
/// <param name="OccurredAt">Instant of the transition (UTC).</param>
public sealed record StatusTransition(
    BookingStatus? From,
    BookingStatus To,
    Actor Actor,
    Channel? Channel,
    string? Reason,
    DateTimeOffset OccurredAt);
