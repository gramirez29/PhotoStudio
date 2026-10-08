using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.UnitTests.Support;

/// <summary>
/// Builds bookings in the states the handler tests need, all created at <see cref="CreatedAt"/> for a two-hour session
/// that starts at <see cref="SessionStart"/>.
/// </summary>
internal static class BookingFactory
{
    /// <summary>
    /// Instant at which every booking is created.
    /// </summary>
    public static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Start of the session of every booking.
    /// </summary>
    public static readonly DateTimeOffset SessionStart = CreatedAt.AddDays(10);

    /// <summary>
    /// Creates a tentative booking with the default policy and a price of 100 000 colones.
    /// </summary>
    /// <returns>The booking.</returns>
    public static Booking Tentative() => Booking.Create(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        ClientContact.Create("María Pérez", "+506 8888-8888"),
        "Retrato familiar",
        Money.Create(100_000m, "CRC"),
        TimeSlot.Create(SessionStart, SessionStart.AddHours(2)),
        BookingPolicy.Default,
        CreatedAt);

    /// <summary>
    /// Creates a confirmed booking: contract signed and the deposit (50 000 colones) paid in person.
    /// </summary>
    /// <returns>The booking.</returns>
    public static Booking Confirmed()
    {
        var booking = Tentative();
        booking.SignContract("María Pérez", "v1", Actor.Photographer, Channel.InPerson, CreatedAt);
        booking.RecordInPersonPayment(Guid.CreateVersion7(), Money.Create(50_000m, "CRC"), PaymentMethod.Cash, "deposit-1", CreatedAt);
        return booking;
    }

    /// <summary>
    /// Creates a confirmed booking whose client was marked absent once the tolerance of the policy passed.
    /// </summary>
    /// <returns>The booking.</returns>
    public static Booking ClientAbsent()
    {
        var booking = Confirmed();
        booking.MarkClientAbsent(SessionStart.AddMinutes(BookingPolicy.Default.ClientAbsentToleranceMinutes));
        return booking;
    }
}
