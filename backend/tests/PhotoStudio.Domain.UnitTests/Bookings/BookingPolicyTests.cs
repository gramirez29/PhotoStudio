using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.UnitTests.Bookings;

/// <summary>
/// Tests of <see cref="BookingPolicy"/>: the defaults of the business and the validation of every value, including the ones that
/// say what is kept or returned when a booking ends and how long the photographer has to deliver.
/// </summary>
public sealed class BookingPolicyTests
{
    /// <summary>
    /// The default policy keeps the whole deposit in both cases of the client, returns everything when the photographer cancels, and
    /// gives 21 days to deliver.
    /// </summary>
    [Fact]
    public void Default_HasTheBusinessDefaults()
    {
        var policy = BookingPolicy.Default;

        policy.LateCancellationRetention.ShouldBe(1m);
        policy.ClientAbsentRetention.ShouldBe(1m);
        policy.PhotographerCancellationRefund.ShouldBe(1m);
        policy.DeliveryDeadlineDays.ShouldBe(21);
    }

    /// <summary>
    /// A policy created with only the older values gets the same defaults, so code that predates the new values is not affected.
    /// </summary>
    [Fact]
    public void Create_WithoutTheNewValues_UsesTheDefaults()
    {
        var policy = BookingPolicy.Create(48, 0.5m, 72, 48, 1, 30, 7);

        policy.ShouldBe(BookingPolicy.Default);
    }

    /// <summary>
    /// The new values are stored as given.
    /// </summary>
    [Fact]
    public void Create_StoresTheNewValues()
    {
        var policy = BookingPolicy.Create(48, 0.5m, 72, 48, 1, 30, 7, 0.5m, 0m, 0.8m, 14);

        policy.LateCancellationRetention.ShouldBe(0.5m);
        policy.ClientAbsentRetention.ShouldBe(0m);
        policy.PhotographerCancellationRefund.ShouldBe(0.8m);
        policy.DeliveryDeadlineDays.ShouldBe(14);
    }

    /// <summary>
    /// A share outside 0 to 1, or a deadline outside 1 to the maximum, is not a policy.
    /// </summary>
    /// <param name="late">Late cancellation retention.</param>
    /// <param name="absent">Client absent retention.</param>
    /// <param name="refund">Photographer cancellation refund.</param>
    /// <param name="days">Delivery deadline in days.</param>
    [Theory]
    [InlineData(-0.01, 1, 1, 21)]
    [InlineData(1.01, 1, 1, 21)]
    [InlineData(1, -0.01, 1, 21)]
    [InlineData(1, 1.01, 1, 21)]
    [InlineData(1, 1, -0.01, 21)]
    [InlineData(1, 1, 1.01, 21)]
    [InlineData(1, 1, 1, 0)]
    [InlineData(1, 1, 1, -3)]
    [InlineData(1, 1, 1, 366)]
    public void Create_WithAValueOutOfRange_IsRefused(double late, double absent, double refund, int days)
    {
        var exception = Should.Throw<DomainException>(() =>
            BookingPolicy.Create(48, 0.5m, 72, 48, 1, 30, 7, (decimal)late, (decimal)absent, (decimal)refund, days));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidPolicy);
    }

    /// <summary>
    /// The limits themselves are valid: nothing kept, everything kept, one day, a year.
    /// </summary>
    [Fact]
    public void Create_AtTheLimits_IsAccepted()
    {
        Should.NotThrow(() => BookingPolicy.Create(48, 0.5m, 72, 48, 1, 30, 7, 0m, 1m, 0m, 1));
        Should.NotThrow(() => BookingPolicy.Create(48, 0.5m, 72, 48, 1, 30, 7, 1m, 0m, 1m, BookingPolicy.MaxDeliveryDeadlineDays));
    }

    /// <summary>
    /// A booking keeps the policy it was created with: the values travel into it and do not change afterwards.
    /// </summary>
    [Fact]
    public void Booking_KeepsTheFrozenPolicy()
    {
        var policy = BookingPolicy.Create(48, 0.5m, 72, 48, 1, 30, 7, 0.5m, 0.25m, 0.9m, 10);

        var booking = BookingTestData.CreateTentative(policy);

        booking.Policy.ShouldBe(policy);
    }
}
