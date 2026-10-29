using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using static PhotoStudio.Domain.UnitTests.Bookings.BookingTestData;

namespace PhotoStudio.Domain.UnitTests.Bookings;

/// <summary>
/// Tests of the price of a booking: the package, the extras sold with it and the discount, and the total the client pays.
/// </summary>
public sealed class BookingPricingTests
{
    /// <summary>
    /// Builds an extra priced in colones.
    /// </summary>
    /// <param name="code">Code of the extra.</param>
    /// <param name="unitPrice">Price of one unit.</param>
    /// <param name="quantity">Number of units.</param>
    /// <returns>The extra.</returns>
    private static BookingExtra Extra(string code, decimal unitPrice, int quantity) =>
        BookingExtra.Create(code, $"Extra {code}", Money.Create(unitPrice), quantity);

    /// <summary>
    /// Creates a booking with the given pieces of price.
    /// </summary>
    /// <param name="extras">Extras sold with it.</param>
    /// <param name="discount">Discount given.</param>
    /// <returns>The booking.</returns>
    private static Booking Create(IEnumerable<BookingExtra>? extras = null, decimal? discount = null)
    {
        var start = SessionStart;
        return Booking.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            ClientContact.Create("María Pérez", "+506 8888-8888"),
            "Retrato familiar",
            Money.Create(PackagePrice),
            TimeSlot.Create(start, start.AddHours(2)),
            BookingPolicy.Default,
            Now,
            PackageTier.Standard,
            extras,
            discount is null ? null : Money.Create(discount.Value));
    }

    /// <summary>
    /// What the client pays is the package plus the extras minus the discount, and the deposit is worked out from that total.
    /// </summary>
    [Fact]
    public void Create_WithExtrasAndDiscount_ChargesPackagePlusExtrasMinusDiscount()
    {
        var booking = Create([Extra("extra-hour", 15_000m, 2), Extra("makeup", 20_000m, 1)], discount: 10_000m);

        booking.BasePrice.Amount.ShouldBe(100_000m);
        booking.ExtrasTotal.Amount.ShouldBe(50_000m);
        booking.PackagePrice.Amount.ShouldBe(140_000m);
        booking.DepositRequired.Amount.ShouldBe(70_000m);
        booking.Balance.Amount.ShouldBe(140_000m);
        booking.PackageTier.ShouldBe(PackageTier.Standard);
    }

    /// <summary>
    /// Without extras or discount the booking is priced exactly as before.
    /// </summary>
    [Fact]
    public void Create_WithoutExtrasOrDiscount_ChargesThePackage()
    {
        var booking = CreateTentative();

        booking.PackagePrice.Amount.ShouldBe(PackagePrice);
        booking.Extras.ShouldBeEmpty();
        booking.Discount.Amount.ShouldBe(0m);
        booking.PackageTier.ShouldBeNull();
    }

    /// <summary>
    /// A discount larger than the package and the extras together would leave a negative price.
    /// </summary>
    [Fact]
    public void Create_WithDiscountLargerThanThePrice_Throws()
    {
        var exception = Should.Throw<DomainException>(() => Create([Extra("makeup", 20_000m, 1)], discount: 120_001m));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidDiscount);
    }

    /// <summary>
    /// A discount equal to the whole price is allowed: the session is a gift.
    /// </summary>
    [Fact]
    public void Create_WithDiscountEqualToThePrice_LeavesNothingToPay()
    {
        var booking = Create(discount: PackagePrice);

        booking.PackagePrice.Amount.ShouldBe(0m);
    }

    /// <summary>
    /// The same extra listed twice is rejected; the quantity is how several units are sold.
    /// </summary>
    [Fact]
    public void Create_WithARepeatedExtra_Throws()
    {
        var exception = Should.Throw<DomainException>(() => Create([Extra("makeup", 20_000m, 1), Extra("makeup", 20_000m, 1)]));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidExtra);
    }

    /// <summary>
    /// Extras and discount must use the currency of the package.
    /// </summary>
    [Fact]
    public void Create_WithAnotherCurrency_Throws()
    {
        var dollars = BookingExtra.Create("makeup", "Maquillaje", Money.Create(50m, "USD"), 1);

        Should.Throw<DomainException>(() => Create([dollars])).Code.ShouldBe(DomainErrorCodes.CurrencyMismatch);
    }

    /// <summary>
    /// An extra needs a code, a name and a quantity from 1 to 99.
    /// </summary>
    /// <param name="code">Code of the extra.</param>
    /// <param name="name">Name of the extra.</param>
    /// <param name="quantity">Number of units.</param>
    [Theory]
    [InlineData("", "Maquillaje", 1)]
    [InlineData("makeup", " ", 1)]
    [InlineData("makeup", "Maquillaje", 0)]
    [InlineData("makeup", "Maquillaje", 100)]
    public void BookingExtra_WithInvalidValues_Throws(string code, string name, int quantity)
    {
        var exception = Should.Throw<DomainException>(() => BookingExtra.Create(code, name, Money.Create(1_000m), quantity));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidExtra);
    }

    /// <summary>
    /// The total of an extra is its unit price times its quantity.
    /// </summary>
    [Fact]
    public void BookingExtra_Total_IsUnitPriceTimesQuantity()
    {
        Extra("print", 2_500m, 4).Total.Amount.ShouldBe(10_000m);
    }

    /// <summary>
    /// Editing the package price moves the total, keeps the extras and the discount, and checks that the discount still fits.
    /// </summary>
    [Fact]
    public void EditDetails_ChangingThePackagePrice_KeepsTheExtrasAndTheDiscount()
    {
        var booking = Create([Extra("makeup", 20_000m, 1)], discount: 10_000m);

        booking.EditDetails(booking.Client, booking.PackageName, Money.Create(150_000m), Now.AddHours(1));

        booking.BasePrice.Amount.ShouldBe(150_000m);
        booking.PackagePrice.Amount.ShouldBe(160_000m);
        booking.Extras.Count.ShouldBe(1);
        booking.Discount.Amount.ShouldBe(10_000m);
    }

    /// <summary>
    /// Lowering the package price below the discount is refused, so the total never goes negative.
    /// </summary>
    [Fact]
    public void EditDetails_LoweringThePriceBelowTheDiscount_Throws()
    {
        var booking = Create(discount: 50_000m);

        var exception = Should.Throw<DomainException>(() => booking.EditDetails(booking.Client, booking.PackageName, Money.Create(40_000m), Now.AddHours(1)));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidDiscount);
        booking.BasePrice.Amount.ShouldBe(PackagePrice);
    }
}
