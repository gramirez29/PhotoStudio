using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Portal;
using PhotoStudio.Application.Portal.CreateLink;
using PhotoStudio.Domain.Portal;

namespace PhotoStudio.Application.UnitTests.Portal;

/// <summary>
/// Tests of <see cref="CreateBookingLinkHandler"/>.
/// </summary>
public sealed class CreateBookingLinkHandlerTests
{
    private readonly PortalFixture _fixture = new();

    /// <summary>
    /// The secret is returned once in the URL, and only its hash is stored.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_ReturnsTheUrlAndStoresOnlyTheHash()
    {
        var response = await NewHandler().HandleAsync(
            new CreateBookingLinkCommand(_fixture.Booking.PhotographerId, _fixture.Booking.Id),
            TestContext.Current.CancellationToken);

        response.Token.ShouldBe("new-token");
        response.Url.ShouldBe("https://book.example.test/b/new-token");
        response.ExpiresAt.ShouldBe(_fixture.Booking.Slot.End + PortalSettings.LinkLifetimeAfterSession);
        await _fixture.Links.Received(1).ReplaceActiveAsync(
            Arg.Is<BookingLink>(link => link.TokenHash == "hash-of-new-token" && link.BookingId == _fixture.Booking.Id && link.PhotographerId == _fixture.Booking.PhotographerId),
            PortalFixture.Now,
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A photographer cannot create a link for the booking of another one; it is reported as not found.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_ForABookingOfAnotherPhotographer_ThrowsNotFound()
    {
        await Should.ThrowAsync<NotFoundException>(() => NewHandler().HandleAsync(
            new CreateBookingLinkCommand(Guid.CreateVersion7(), _fixture.Booking.Id),
            TestContext.Current.CancellationToken));

        await _fixture.Links.DidNotReceive().ReplaceActiveAsync(Arg.Any<BookingLink>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Builds the handler on the substituted ports.
    /// </summary>
    /// <returns>The handler.</returns>
    private CreateBookingLinkHandler NewHandler() =>
        new(_fixture.Bookings, _fixture.Links, _fixture.Tokens, _fixture.Settings, _fixture.Clock);
}
