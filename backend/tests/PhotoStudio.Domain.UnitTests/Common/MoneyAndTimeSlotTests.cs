using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.UnitTests.Common;

/// <summary>
/// Tests of the <see cref="Money"/> and <see cref="TimeSlot"/> value objects.
/// </summary>
public sealed class MoneyAndTimeSlotTests
{
    /// <summary>
    /// Negative amounts are rejected.
    /// </summary>
    [Fact]
    public void Money_Negative_Throws()
    {
        var exception = Should.Throw<DomainException>(() => Money.Create(-1m));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidAmount);
    }

    /// <summary>
    /// Currency codes are normalized to upper case.
    /// </summary>
    [Fact]
    public void Money_NormalizesCurrency()
    {
        Money.Create(10m, " usd ").Currency.ShouldBe("USD");
    }

    /// <summary>
    /// Invalid currency codes are rejected.
    /// </summary>
    [Fact]
    public void Money_InvalidCurrency_Throws()
    {
        var exception = Should.Throw<DomainException>(() => Money.Create(10m, "COLONES"));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidCurrency);
    }

    /// <summary>
    /// Amounts in different currencies cannot be combined.
    /// </summary>
    [Fact]
    public void Money_AddDifferentCurrencies_Throws()
    {
        var exception = Should.Throw<DomainException>(() => Money.Create(10m, "CRC").Add(Money.Create(10m, "USD")));

        exception.Code.ShouldBe(DomainErrorCodes.CurrencyMismatch);
    }

    /// <summary>
    /// Multiplication rounds to two decimals.
    /// </summary>
    [Fact]
    public void Money_MultiplyBy_RoundsToTwoDecimals()
    {
        Money.Create(10.01m).MultiplyBy(0.5m).Amount.ShouldBe(5.01m);
    }

    /// <summary>
    /// A slot must end after it starts.
    /// </summary>
    [Fact]
    public void TimeSlot_EndBeforeStart_Throws()
    {
        var start = new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

        var exception = Should.Throw<DomainException>(() => TimeSlot.Create(start, start));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidTimeSlot);
    }

    /// <summary>
    /// Slots that only touch at the boundary do not overlap; slots that share time do.
    /// </summary>
    [Fact]
    public void TimeSlot_Overlaps_UsesHalfOpenIntervals()
    {
        var start = new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);
        var morning = TimeSlot.Create(start, start.AddHours(2));
        var adjacent = TimeSlot.Create(start.AddHours(2), start.AddHours(3));
        var overlapping = TimeSlot.Create(start.AddHours(1), start.AddHours(3));

        morning.Overlaps(adjacent).ShouldBeFalse();
        morning.Overlaps(overlapping).ShouldBeTrue();
    }

    /// <summary>
    /// Slots are normalized to UTC regardless of the offset they were created with.
    /// </summary>
    [Fact]
    public void TimeSlot_NormalizesToUtc()
    {
        var costaRicaOffset = TimeSpan.FromHours(-6);
        var start = new DateTimeOffset(2026, 10, 10, 9, 0, 0, costaRicaOffset);

        var slot = TimeSlot.Create(start, start.AddHours(1));

        slot.Start.Offset.ShouldBe(TimeSpan.Zero);
        slot.Start.ShouldBe(start);
    }
}
