using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.UnitTests.Bookings;

/// <summary>
/// Builders and fixed values shared by the booking tests.
/// </summary>
internal static class BookingTestData
{
    /// <summary>Fixed "current" instant used by every test.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Default session start: ten days after <see cref="Now"/>.</summary>
    public static readonly DateTimeOffset SessionStart = Now.AddDays(10);

    /// <summary>Default package price: ₡100 000 (deposit at 50 % = ₡50 000).</summary>
    public const decimal PackagePrice = 100_000m;

    /// <summary>
    /// Creates a tentative booking with the default policy unless another one is given.
    /// </summary>
    /// <param name="policy">Policy to copy into the booking.</param>
    /// <param name="sessionStart">Session start; defaults to <see cref="SessionStart"/>.</param>
    /// <returns>A tentative booking.</returns>
    public static Booking CreateTentative(BookingPolicy? policy = null, DateTimeOffset? sessionStart = null)
    {
        var start = sessionStart ?? SessionStart;
        return Booking.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            ClientContact.Create("María Pérez", "+506 8888-8888"),
            "Retrato familiar",
            Money.Create(PackagePrice),
            TimeSlot.Create(start, start.AddHours(2)),
            policy ?? BookingPolicy.Default,
            Now);
    }

    /// <summary>
    /// Creates a booking that is already confirmed: contract signed in person and deposit paid in cash.
    /// </summary>
    /// <param name="policy">Policy to copy into the booking.</param>
    /// <returns>A confirmed booking.</returns>
    public static Booking CreateConfirmed(BookingPolicy? policy = null)
    {
        var booking = CreateTentative(policy);
        booking.SignContract("María Pérez", "v1", Actor.Photographer, Channel.InPerson, Now);
        booking.RecordInPersonPayment(Guid.CreateVersion7(), Money.Create(PackagePrice / 2), PaymentMethod.Cash, "deposit-1", Now);
        return booking;
    }

    /// <summary>
    /// Creates a policy identical to the default one except for the given overrides.
    /// </summary>
    /// <param name="maxReschedules">Maximum client reschedules.</param>
    /// <param name="depositPercentage">Deposit fraction.</param>
    /// <returns>The policy.</returns>
    public static BookingPolicy Policy(int maxReschedules = 1, decimal depositPercentage = 0.5m) =>
        BookingPolicy.Create(48, depositPercentage, 72, 48, maxReschedules, 30, 7);
}
