using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.CancelBooking;
using PhotoStudio.Application.Bookings.CompleteBooking;
using PhotoStudio.Application.Bookings.GetBooking;
using PhotoStudio.Application.Bookings.MarkClientAbsent;
using PhotoStudio.Application.Bookings.RecordInPersonPayment;
using PhotoStudio.Application.Bookings.RescheduleBooking;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Bookings.RevertClientAbsent;
using PhotoStudio.Application.Bookings.SignContractInPerson;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.UnitTests.Bookings;

/// <summary>
/// Tenant isolation: a photographer can neither read nor change the booking of another photographer. The booking of someone
/// else is reported exactly like a missing one (<see cref="NotFoundException"/>), and nothing is saved.
/// </summary>
public sealed class BookingTenantIsolationTests
{
    private static readonly DateTimeOffset Later = BookingFactory.SessionStart.AddHours(3);

    private readonly IBookingRepository _repository = Substitute.For<IBookingRepository>();
    private readonly TimeProvider _clock = new FixedTimeProvider(Later);

    /// <summary>
    /// Another photographer cannot read the booking.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task GetBooking_OfAnotherPhotographer_IsNotFound()
    {
        var booking = StoredBooking();
        var handler = new GetBookingHandler(_repository, _clock);

        var exception = await Should.ThrowAsync<NotFoundException>(
            () => handler.HandleAsync(new GetBookingQuery(Stranger(), booking.Id), TestContext.Current.CancellationToken));

        exception.ResourceId.ShouldBe(booking.Id);
    }

    /// <summary>
    /// The owner reads the booking normally.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task GetBooking_OfTheOwner_ReturnsTheBooking()
    {
        var booking = StoredBooking();
        var handler = new GetBookingHandler(_repository, _clock);

        var response = await handler.HandleAsync(new GetBookingQuery(booking.PhotographerId, booking.Id), TestContext.Current.CancellationToken);

        response.Id.ShouldBe(booking.Id);
    }

    /// <summary>
    /// Another photographer cannot cancel the booking.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Cancel_OfAnotherPhotographer_IsNotFoundAndSavesNothing()
    {
        var booking = StoredBooking();
        var handler = new CancelBookingHandler(_repository, _clock);

        await Should.ThrowAsync<NotFoundException>(
            () => handler.HandleAsync(new CancelBookingCommand(Stranger(), booking.Id, "x"), TestContext.Current.CancellationToken));

        await NothingSavedAsync();
        booking.Status.ShouldBe(BookingStatus.Confirmed);
    }

    /// <summary>
    /// Another photographer cannot complete the booking.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Complete_OfAnotherPhotographer_IsNotFoundAndSavesNothing()
    {
        var booking = StoredBooking();
        var handler = new CompleteBookingHandler(_repository, _clock);

        await Should.ThrowAsync<NotFoundException>(
            () => handler.HandleAsync(new CompleteBookingCommand(Stranger(), booking.Id), TestContext.Current.CancellationToken));

        await NothingSavedAsync();
        booking.Status.ShouldBe(BookingStatus.Confirmed);
    }

    /// <summary>
    /// Another photographer cannot mark the client absent.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task MarkClientAbsent_OfAnotherPhotographer_IsNotFoundAndSavesNothing()
    {
        var booking = StoredBooking();
        var handler = new MarkClientAbsentHandler(_repository, _clock);

        await Should.ThrowAsync<NotFoundException>(
            () => handler.HandleAsync(new MarkClientAbsentCommand(Stranger(), booking.Id), TestContext.Current.CancellationToken));

        await NothingSavedAsync();
        booking.Status.ShouldBe(BookingStatus.Confirmed);
    }

    /// <summary>
    /// Another photographer cannot revert a client-absent mark.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RevertClientAbsent_OfAnotherPhotographer_IsNotFoundAndSavesNothing()
    {
        var booking = BookingFactory.ClientAbsent();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        var handler = new RevertClientAbsentHandler(_repository, _clock);

        await Should.ThrowAsync<NotFoundException>(
            () => handler.HandleAsync(new RevertClientAbsentCommand(Stranger(), booking.Id, "error"), TestContext.Current.CancellationToken));

        await NothingSavedAsync();
        booking.Status.ShouldBe(BookingStatus.ClientAbsent);
    }

    /// <summary>
    /// Another photographer cannot reschedule the booking.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Reschedule_OfAnotherPhotographer_IsNotFoundAndSavesNothing()
    {
        var booking = StoredBooking();
        var handler = new RescheduleBookingHandler(_repository, _clock);
        var newStart = Later.AddDays(5);

        await Should.ThrowAsync<NotFoundException>(
            () => handler.HandleAsync(
                new RescheduleBookingCommand(Stranger(), booking.Id, newStart, newStart.AddHours(2)),
                TestContext.Current.CancellationToken));

        await _repository.DidNotReceive().UpdateReservingSlotAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Another photographer cannot sign the contract of the booking.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task SignContract_OfAnotherPhotographer_IsNotFoundAndSavesNothing()
    {
        var booking = BookingFactory.Tentative();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        var handler = new SignContractInPersonHandler(_repository, _clock);

        await Should.ThrowAsync<NotFoundException>(
            () => handler.HandleAsync(
                new SignContractInPersonCommand(Stranger(), booking.Id, "María Pérez", "v1", false),
                TestContext.Current.CancellationToken));

        await NothingSavedAsync();
        booking.Contract.ShouldBeNull();
    }

    /// <summary>
    /// Another photographer cannot record a payment on the booking.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RecordPayment_OfAnotherPhotographer_IsNotFoundAndSavesNothing()
    {
        var booking = BookingFactory.Tentative();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        var handler = new RecordInPersonPaymentHandler(_repository, _clock);

        await Should.ThrowAsync<NotFoundException>(
            () => handler.HandleAsync(
                new RecordInPersonPaymentCommand(Stranger(), booking.Id, 50_000m, "CRC", PaymentMethod.Cash, "key-1"),
                TestContext.Current.CancellationToken));

        await NothingSavedAsync();
        booking.Payments.ShouldBeEmpty();
    }

    /// <summary>
    /// Creates a confirmed booking and makes the repository return it.
    /// </summary>
    /// <returns>The booking.</returns>
    private Booking StoredBooking()
    {
        var booking = BookingFactory.Confirmed();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        return booking;
    }

    /// <summary>
    /// Builds the identifier of a photographer who owns nothing.
    /// </summary>
    /// <returns>A new identifier.</returns>
    private static Guid Stranger() => Guid.CreateVersion7();

    /// <summary>
    /// Asserts that no write reached the repository.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    private async Task NothingSavedAsync() =>
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
}
