using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.GetAttention;
using PhotoStudio.Application.UnitTests.Support;

namespace PhotoStudio.Application.UnitTests.Bookings;

/// <summary>
/// Tests of <see cref="GetBookingAttentionHandler"/>: what is waiting for the photographer, for the home screen.
/// </summary>
public sealed class GetBookingAttentionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 16, 0, 0, TimeSpan.Zero);

    private readonly IBookingRepository _repository = Substitute.For<IBookingRepository>();

    /// <summary>
    /// The answer carries both counts, and the holds that count as about to expire are the ones that end within 24 hours from now.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_ReportsBothCounts_AndLooksOneDayAhead()
    {
        var photographerId = Guid.CreateVersion7();
        _repository.CountWithPendingProofAsync(photographerId, Arg.Any<CancellationToken>()).Returns(2);
        _repository.CountExpiringTentativeAsync(photographerId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(3);

        var response = await new GetBookingAttentionHandler(_repository, new FixedTimeProvider(Now))
            .HandleAsync(new GetBookingAttentionQuery(photographerId), TestContext.Current.CancellationToken);

        response.ShouldBe(new BookingAttentionResponse(2, 3));
        await _repository.Received(1).CountExpiringTentativeAsync(photographerId, Now.AddHours(24), Arg.Any<CancellationToken>());
    }
}
