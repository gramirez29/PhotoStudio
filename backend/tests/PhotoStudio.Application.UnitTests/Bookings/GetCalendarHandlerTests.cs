using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.GetCalendar;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.UnitTests.Bookings;

/// <summary>
/// Tests of <see cref="GetCalendarHandler"/>: the bookings of a range for the calendar of the app.
/// </summary>
public sealed class GetCalendarHandlerTests
{
    private static readonly DateTimeOffset From = new(2026, 11, 1, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = From.AddDays(7);

    private readonly IBookingRepository _repository = Substitute.For<IBookingRepository>();

    /// <summary>
    /// By default only the bookings that hold the schedule are asked for, and one more than the answer can carry, to know if some were left out.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_ByDefault_AsksOnlyForTentativeAndConfirmed()
    {
        var photographerId = Guid.CreateVersion7();
        _repository.ListInRangeAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<IReadOnlyCollection<BookingStatus>?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);

        var response = await new GetCalendarHandler(_repository).HandleAsync(new GetCalendarQuery(photographerId, From, To), TestContext.Current.CancellationToken);

        response.Items.ShouldBeEmpty();
        response.Truncated.ShouldBeFalse();
        await _repository.Received(1).ListInRangeAsync(
            photographerId,
            From,
            To,
            Arg.Is<IReadOnlyCollection<BookingStatus>?>(statuses => statuses != null && statuses.Count == 2 && statuses.Contains(BookingStatus.Tentative) && statuses.Contains(BookingStatus.Confirmed)),
            CalendarLimits.MaxItems + 1,
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Asking for the bookings that are over lifts the status filter.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithIncludeEnded_AsksForEveryStatus()
    {
        var photographerId = Guid.CreateVersion7();
        _repository.ListInRangeAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<IReadOnlyCollection<BookingStatus>?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);

        await new GetCalendarHandler(_repository).HandleAsync(new GetCalendarQuery(photographerId, From, To, IncludeEnded: true), TestContext.Current.CancellationToken);

        await _repository.Received(1).ListInRangeAsync(photographerId, From, To, null, CalendarLimits.MaxItems + 1, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The bookings come back as summaries, in the order the repository gave them.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_MapsTheBookingsToSummaries()
    {
        var first = BookingFactory.Confirmed();
        var second = BookingFactory.Tentative();
        _repository.ListInRangeAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<IReadOnlyCollection<BookingStatus>?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([first, second]);

        var response = await new GetCalendarHandler(_repository).HandleAsync(new GetCalendarQuery(first.PhotographerId, From, To), TestContext.Current.CancellationToken);

        response.Items.Select(item => item.Id).ShouldBe([first.Id, second.Id]);
        response.Items[0].Status.ShouldBe(BookingStatus.Confirmed);
        response.Items[0].ClientName.ShouldBe("María Pérez");
        response.Truncated.ShouldBeFalse();
    }

    /// <summary>
    /// When the repository returns the extra booking, the answer carries the maximum and says that later ones were left out.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithMoreThanTheMaximum_CutsTheAnswerAndSaysSo()
    {
        var bookings = Enumerable.Range(0, CalendarLimits.MaxItems + 1).Select(_ => BookingFactory.Tentative()).ToList();
        _repository.ListInRangeAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<IReadOnlyCollection<BookingStatus>?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(bookings);

        var response = await new GetCalendarHandler(_repository).HandleAsync(new GetCalendarQuery(Guid.CreateVersion7(), From, To), TestContext.Current.CancellationToken);

        response.Items.Count.ShouldBe(CalendarLimits.MaxItems);
        response.Truncated.ShouldBeTrue();
        response.Items.Select(item => item.Id).ShouldNotContain(bookings[^1].Id);
    }

    /// <summary>
    /// Exactly the maximum is not "too many".
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithExactlyTheMaximum_IsNotTruncated()
    {
        var bookings = Enumerable.Range(0, CalendarLimits.MaxItems).Select(_ => BookingFactory.Tentative()).ToList();
        _repository.ListInRangeAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<IReadOnlyCollection<BookingStatus>?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(bookings);

        var response = await new GetCalendarHandler(_repository).HandleAsync(new GetCalendarQuery(Guid.CreateVersion7(), From, To), TestContext.Current.CancellationToken);

        response.Items.Count.ShouldBe(CalendarLimits.MaxItems);
        response.Truncated.ShouldBeFalse();
    }

    /// <summary>
    /// An empty, inverted or too long range is refused before the database is asked.
    /// </summary>
    /// <param name="days">Length of the range in days; negative for an inverted one.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(63)]
    public async Task HandleAsync_WithAnInvalidRange_IsRefused(int days)
    {
        await Should.ThrowAsync<ArgumentException>(() =>
            new GetCalendarHandler(_repository).HandleAsync(new GetCalendarQuery(Guid.CreateVersion7(), From, From.AddDays(days)), TestContext.Current.CancellationToken));

        await _repository.DidNotReceive().ListInRangeAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<IReadOnlyCollection<BookingStatus>?>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The longest range is accepted.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithTheLongestRange_IsAccepted()
    {
        _repository.ListInRangeAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<IReadOnlyCollection<BookingStatus>?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);

        await Should.NotThrowAsync(() =>
            new GetCalendarHandler(_repository).HandleAsync(new GetCalendarQuery(Guid.CreateVersion7(), From, From.AddDays(CalendarLimits.MaxRangeDays)), TestContext.Current.CancellationToken));
    }
}
