using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.CancelBooking;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.UnitTests.Bookings;

/// <summary>
/// Tests of <see cref="CancelBookingHandler"/>.
/// </summary>
public sealed class CancelBookingHandlerTests
{
    private readonly IBookingRepository _repository = Substitute.For<IBookingRepository>();
    private readonly CancelBookingHandler _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="CancelBookingHandlerTests"/> class.
    /// </summary>
    public CancelBookingHandlerTests()
    {
        _handler = new CancelBookingHandler(_repository, new FixedTimeProvider(BookingFactory.CreatedAt.AddDays(1)));
    }

    /// <summary>
    /// A missing booking produces <see cref="NotFoundException"/>.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithUnknownBooking_ThrowsNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Booking?)null);

        await Should.ThrowAsync<NotFoundException>(
            () => _handler.HandleAsync(new CancelBookingCommand(Guid.CreateVersion7(), Guid.CreateVersion7(), null), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A tentative booking can be cancelled without a reason.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithTentativeBookingAndNoReason_CancelsAndSaves()
    {
        var booking = BookingFactory.Tentative();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        var response = await _handler.HandleAsync(new CancelBookingCommand(booking.PhotographerId, booking.Id, null), TestContext.Current.CancellationToken);

        response.Status.ShouldBe(BookingStatus.Cancelled);
        response.AllowedActions.ShouldBeEmpty();
        await _repository.Received(1).UpdateAsync(booking, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A confirmed booking needs a reason, and nothing is saved without it.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithConfirmedBookingAndNoReason_ThrowsReasonRequired()
    {
        var booking = BookingFactory.Confirmed();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        var exception = await Should.ThrowAsync<DomainException>(
            () => _handler.HandleAsync(new CancelBookingCommand(booking.PhotographerId, booking.Id, "  "), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.ReasonRequired);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A confirmed booking is cancelled when a reason is given.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithConfirmedBookingAndReason_CancelsAndSaves()
    {
        var booking = BookingFactory.Confirmed();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        var response = await _handler.HandleAsync(
            new CancelBookingCommand(booking.PhotographerId, booking.Id, "El cliente pidió cancelar"),
            TestContext.Current.CancellationToken);

        response.Status.ShouldBe(BookingStatus.Cancelled);
        await _repository.Received(1).UpdateAsync(booking, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A booking that is already cancelled cannot be cancelled again.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAlreadyCancelledBooking_ThrowsInvalidTransition()
    {
        var booking = BookingFactory.Tentative();
        booking.Cancel(Actor.Photographer, null, BookingFactory.CreatedAt);
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        var exception = await Should.ThrowAsync<DomainException>(
            () => _handler.HandleAsync(new CancelBookingCommand(booking.PhotographerId, booking.Id, "otra vez"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidTransition);
    }
}
