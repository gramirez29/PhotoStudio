using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.GetBooking;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.UnitTests.Bookings;

/// <summary>
/// Tests of <see cref="GetBookingHandler"/>.
/// </summary>
public sealed class GetBookingHandlerTests
{
    /// <summary>
    /// A missing booking produces <see cref="NotFoundException"/> with the requested identifier.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithUnknownBooking_ThrowsNotFound()
    {
        var repository = Substitute.For<IBookingRepository>();
        repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Booking?)null);
        var handler = new GetBookingHandler(repository, new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        var bookingId = Guid.CreateVersion7();

        var exception = await Should.ThrowAsync<NotFoundException>(
            () => handler.HandleAsync(new GetBookingQuery(Guid.CreateVersion7(), bookingId), TestContext.Current.CancellationToken));

        exception.ResourceId.ShouldBe(bookingId);
    }
}
