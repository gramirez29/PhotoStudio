using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.CreateBooking;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.UnitTests.Bookings;

/// <summary>
/// Tests of <see cref="CreateBookingHandler"/>.
/// </summary>
public sealed class CreateBookingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IBookingRepository _repository = Substitute.For<IBookingRepository>();
    private readonly IBookingPolicyProvider _policyProvider = Substitute.For<IBookingPolicyProvider>();
    private readonly CreateBookingHandler _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateBookingHandlerTests"/> class with a free slot and the default policy.
    /// </summary>
    public CreateBookingHandlerTests()
    {
        _policyProvider.GetPolicyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(BookingPolicy.Default);
        _handler = new CreateBookingHandler(_repository, _policyProvider, new FixedTimeProvider(Now));
    }

    /// <summary>
    /// A free slot produces a tentative booking that is persisted once.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithFreeSlot_PersistsTentativeBooking()
    {
        var response = await _handler.HandleAsync(ValidCommand(), TestContext.Current.CancellationToken);

        response.Status.ShouldBe(BookingStatus.Tentative);
        response.DepositRequired.Amount.ShouldBe(50_000m);
        response.AllowedActions.ShouldContain(BookingAction.RecordInPersonPayment);
        await _repository.Received(1).AddAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An overlapping slot is rejected before anything is persisted.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithTakenSlot_ThrowsConflict()
    {
        _repository
            .HasOverlappingActiveBookingAsync(Arg.Any<Guid>(), Arg.Any<TimeSlot>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var exception = await Should.ThrowAsync<ConflictException>(
            () => _handler.HandleAsync(ValidCommand(), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.SlotUnavailable);
        await _repository.DidNotReceive().AddAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Invalid data is rejected by the domain and nothing is persisted.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithEndBeforeStart_ThrowsDomainException()
    {
        var command = ValidCommand() with { SessionEnd = Now.AddDays(10).AddHours(-1) };

        var exception = await Should.ThrowAsync<DomainException>(
            () => _handler.HandleAsync(command, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidTimeSlot);
        await _repository.DidNotReceive().AddAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Builds a valid command for a session ten days from now.
    /// </summary>
    /// <returns>The command.</returns>
    private static CreateBookingCommand ValidCommand() => new(
        Guid.CreateVersion7(),
        "María Pérez",
        "+506 8888-8888",
        "Retrato familiar",
        100_000m,
        "CRC",
        Now.AddDays(10),
        Now.AddDays(10).AddHours(2));
}
