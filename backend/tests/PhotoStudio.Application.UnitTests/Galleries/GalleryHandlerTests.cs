using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Galleries;
using PhotoStudio.Application.Galleries.EditTerms;
using PhotoStudio.Application.Galleries.GetGallery;
using PhotoStudio.Application.Galleries.ListGalleries;
using PhotoStudio.Application.Pagination;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Bookings.Events;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Application.UnitTests.Galleries;

/// <summary>
/// Tests of the gallery use cases: creating it when a session is completed, reading it and editing its terms.
/// </summary>
public sealed class GalleryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly IBookingRepository _bookings = Substitute.For<IBookingRepository>();
    private readonly IGalleryRepository _galleries = Substitute.For<IGalleryRepository>();
    private readonly IGalleryPolicyProvider _policies = Substitute.For<IGalleryPolicyProvider>();

    /// <summary>
    /// Initializes a new instance of the <see cref="GalleryHandlerTests"/> class with the default policy.
    /// </summary>
    public GalleryHandlerTests()
    {
        _policies.GetPolicyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(GalleryPolicy.Default);
        _galleries.TryAddAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    /// <summary>
    /// Completing the session of a booking creates its gallery with the photos of the package, the price of an extra and the
    /// snapshot of the client, in the currency of the booking.
    /// </summary>
    /// <param name="tier">The package of the booking.</param>
    /// <param name="included">The photos the package includes.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData(PackageTier.Basic, 15)]
    [InlineData(PackageTier.Standard, 30)]
    [InlineData(PackageTier.Premium, 50)]
    [InlineData(PackageTier.Custom, 0)]
    [InlineData(null, 0)]
    public async Task SessionCompleted_CreatesTheGalleryOfThePackage(PackageTier? tier, int included)
    {
        var booking = NewBooking(tier);
        _bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        Gallery? created = null;
        await _galleries.TryAddAsync(Arg.Do<Gallery>(gallery => created = gallery), Arg.Any<CancellationToken>());

        await Handler().HandleAsync(new SessionCompleted(booking.Id, booking.PhotographerId, Now), TestContext.Current.CancellationToken);

        created.ShouldNotBeNull();
        created.BookingId.ShouldBe(booking.Id);
        created.PhotographerId.ShouldBe(booking.PhotographerId);
        created.Status.ShouldBe(GalleryStatus.Pending);
        created.IncludedPhotos.ShouldBe(included);
        created.ExtraPhotoPrice.ShouldBe(Money.Create(3_000m, "CRC"));
        created.Snapshot.ClientName.ShouldBe("María Pérez");
        created.Snapshot.PackageName.ShouldBe("Retrato familiar");
        created.Snapshot.SessionStart.ShouldBe(booking.Slot.Start);
        created.CreatedAt.ShouldBe(Now);
    }

    /// <summary>
    /// The policy that applies is the photographer's, and what it says today is copied into the gallery.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task SessionCompleted_CopiesThePolicyOfThePhotographer()
    {
        var booking = NewBooking(PackageTier.Standard);
        _bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        _policies.GetPolicyAsync(booking.PhotographerId, Arg.Any<CancellationToken>()).Returns(GalleryPolicy.Create(10, 20, 40, 5_000m));
        Gallery? created = null;
        await _galleries.TryAddAsync(Arg.Do<Gallery>(gallery => created = gallery), Arg.Any<CancellationToken>());

        await Handler().HandleAsync(new SessionCompleted(booking.Id, booking.PhotographerId, Now), TestContext.Current.CancellationToken);

        created!.IncludedPhotos.ShouldBe(20);
        created.ExtraPhotoPrice.Amount.ShouldBe(5_000m);
    }

    /// <summary>
    /// The outbox delivers at least once: a second delivery finds the gallery already there and is not an error.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task SessionCompleted_DeliveredTwice_IsNotAnError()
    {
        var booking = NewBooking(PackageTier.Basic);
        _bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        _galleries.TryAddAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>()).Returns(false);

        await Should.NotThrowAsync(() => Handler().HandleAsync(new SessionCompleted(booking.Id, booking.PhotographerId, Now), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// An event about a booking that no longer exists creates nothing and does not fail, so the outbox does not retry it forever.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task SessionCompleted_ForAMissingBooking_CreatesNothing()
    {
        _bookings.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Booking?)null);

        await Handler().HandleAsync(new SessionCompleted(Guid.CreateVersion7(), Guid.CreateVersion7(), Now), TestContext.Current.CancellationToken);

        await _galleries.DidNotReceive().TryAddAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Reading a gallery gives its terms, the allowed actions and the snapshot of the session.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Get_ReturnsTheGalleryWithItsAllowedActions()
    {
        var gallery = NewGallery(GalleryStatus.Pending);
        _galleries.GetByIdAsync(gallery.Id, Arg.Any<CancellationToken>()).Returns(gallery);

        var response = await new GetGalleryHandler(_galleries).HandleAsync(new GetGalleryQuery(gallery.PhotographerId, gallery.Id), TestContext.Current.CancellationToken);

        response.Id.ShouldBe(gallery.Id);
        response.Status.ShouldBe(GalleryStatus.Pending);
        response.IncludedPhotos.ShouldBe(15);
        response.SelectablePhotos.ShouldBe(15);
        response.ExtraPhotoPrice.Amount.ShouldBe(3_000m);
        response.MaxPhotos.ShouldBe(Gallery.MaxPhotos);
        response.ClientName.ShouldBe("María Pérez");
        response.AllowedActions.ShouldBe([GalleryAction.EditTerms, GalleryAction.UploadProofs]);
    }

    /// <summary>
    /// The gallery of another photographer and one that does not exist answer exactly the same.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Get_OfAnotherPhotographerOrMissing_IsNotFound()
    {
        var gallery = NewGallery(GalleryStatus.Pending);
        _galleries.GetByIdAsync(gallery.Id, Arg.Any<CancellationToken>()).Returns(gallery);
        _galleries.GetByIdAsync(Arg.Is<Guid>(id => id != gallery.Id), Arg.Any<CancellationToken>()).Returns((Gallery?)null);
        var handler = new GetGalleryHandler(_galleries);

        await Should.ThrowAsync<NotFoundException>(() => handler.HandleAsync(new GetGalleryQuery(Guid.CreateVersion7(), gallery.Id), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<NotFoundException>(() => handler.HandleAsync(new GetGalleryQuery(gallery.PhotographerId, Guid.CreateVersion7()), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The gallery of a booking is found by the booking, and a booking of another photographer, or one without gallery, is not found.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task GetByBooking_FindsItOnlyForItsOwner()
    {
        var gallery = NewGallery(GalleryStatus.Pending);
        _galleries.GetByBookingIdAsync(gallery.BookingId, Arg.Any<CancellationToken>()).Returns(gallery);
        _galleries.GetByBookingIdAsync(Arg.Is<Guid>(id => id != gallery.BookingId), Arg.Any<CancellationToken>()).Returns((Gallery?)null);
        var handler = new GetGalleryByBookingHandler(_galleries);

        (await handler.HandleAsync(new GetGalleryByBookingQuery(gallery.PhotographerId, gallery.BookingId), TestContext.Current.CancellationToken)).Id.ShouldBe(gallery.Id);
        await Should.ThrowAsync<NotFoundException>(() => handler.HandleAsync(new GetGalleryByBookingQuery(Guid.CreateVersion7(), gallery.BookingId), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<NotFoundException>(() => handler.HandleAsync(new GetGalleryByBookingQuery(gallery.PhotographerId, Guid.CreateVersion7()), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The list asks for one more than the page, cuts the extra one and names the last gallery of the page in the cursor.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task List_WithMoreThanAPage_CutsItAndReturnsTheCursorOfTheLastOne()
    {
        var photographer = Guid.CreateVersion7();
        var found = Enumerable.Range(0, 3).Select(_ => NewGallery(GalleryStatus.Pending, photographer)).ToList();
        _galleries.ListByPhotographerAsync(photographer, null, 3, Arg.Any<CancellationToken>()).Returns(found);

        var page = await new ListGalleriesHandler(_galleries).HandleAsync(new ListGalleriesQuery(photographer, null, 2), TestContext.Current.CancellationToken);

        page.Items.Select(item => item.Id).ShouldBe([found[0].Id, found[1].Id]);
        PageCursor.TryParse(page.NextCursor, out var cursor).ShouldBeTrue();
        cursor.Id.ShouldBe(found[1].Id);
        cursor.At.ShouldBe(found[1].CreatedAt);
    }

    /// <summary>
    /// The last page has no cursor, and the size and the position are passed on to the repository within bounds.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task List_OnTheLastPage_HasNoCursor_AndKeepsTheSizeWithinBounds()
    {
        var photographer = Guid.CreateVersion7();
        var after = new PageCursor(Now, Guid.CreateVersion7());
        _galleries.ListByPhotographerAsync(photographer, after, PageSizes.Max + 1, Arg.Any<CancellationToken>()).Returns([NewGallery(GalleryStatus.Pending, photographer)]);

        var page = await new ListGalleriesHandler(_galleries).HandleAsync(new ListGalleriesQuery(photographer, after, 500), TestContext.Current.CancellationToken);

        page.Items.Count.ShouldBe(1);
        page.NextCursor.ShouldBeNull();
    }

    /// <summary>
    /// Editing the terms changes the gallery, saves it and answers with the new terms, keeping the currency.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task EditTerms_SavesTheNewTerms()
    {
        var gallery = NewGallery(GalleryStatus.Pending);
        _galleries.GetByIdAsync(gallery.Id, Arg.Any<CancellationToken>()).Returns(gallery);

        var response = await new EditGalleryTermsHandler(_galleries, new FixedTimeProvider(Now.AddHours(1)))
            .HandleAsync(new EditGalleryTermsCommand(gallery.PhotographerId, gallery.Id, 30, 5, 2_500m), TestContext.Current.CancellationToken);

        response.IncludedPhotos.ShouldBe(30);
        response.BonusPhotos.ShouldBe(5);
        response.SelectablePhotos.ShouldBe(35);
        response.ExtraPhotoPrice.ShouldBe(new PhotoStudio.Application.Bookings.Responses.MoneyResponse(2_500m, "CRC"));
        await _galleries.Received(1).UpdateAsync(gallery, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Another photographer cannot edit the terms of a gallery, and the answer does not confirm it exists.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task EditTerms_OfAnotherPhotographer_IsNotFoundAndSavesNothing()
    {
        var gallery = NewGallery(GalleryStatus.Pending);
        _galleries.GetByIdAsync(gallery.Id, Arg.Any<CancellationToken>()).Returns(gallery);

        await Should.ThrowAsync<NotFoundException>(() => new EditGalleryTermsHandler(_galleries, new FixedTimeProvider(Now))
            .HandleAsync(new EditGalleryTermsCommand(Guid.CreateVersion7(), gallery.Id, 30, 5, 2_500m), TestContext.Current.CancellationToken));

        gallery.IncludedPhotos.ShouldBe(15);
        await _galleries.DidNotReceive().UpdateAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When the domain refuses the change (the selection was sent, or a value is invalid), nothing is saved.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task EditTerms_RefusedByTheDomain_SavesNothing()
    {
        var frozen = NewGallery(GalleryStatus.InEditing);
        var open = NewGallery(GalleryStatus.Pending);
        _galleries.GetByIdAsync(frozen.Id, Arg.Any<CancellationToken>()).Returns(frozen);
        _galleries.GetByIdAsync(open.Id, Arg.Any<CancellationToken>()).Returns(open);
        var handler = new EditGalleryTermsHandler(_galleries, new FixedTimeProvider(Now));

        await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(new EditGalleryTermsCommand(frozen.PhotographerId, frozen.Id, 30, 0, 3_000m), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(new EditGalleryTermsCommand(open.PhotographerId, open.Id, 30, 0, -1m), TestContext.Current.CancellationToken));

        await _galleries.DidNotReceive().UpdateAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Creates the handler that builds galleries.
    /// </summary>
    /// <returns>The handler.</returns>
    private GalleryCreationHandler Handler() => new(_bookings, _galleries, _policies, new FixedTimeProvider(Now));

    /// <summary>
    /// Builds a booking of a package, for a two-hour session.
    /// </summary>
    /// <param name="tier">The package, if the booking has one.</param>
    /// <returns>The booking.</returns>
    private static Booking NewBooking(PackageTier? tier) => Booking.Create(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        ClientContact.Create("María Pérez", "+506 8888-8888"),
        "Retrato familiar",
        Money.Create(100_000m, "CRC"),
        TimeSlot.Create(Now.AddDays(10), Now.AddDays(10).AddHours(2)),
        BookingPolicy.Default,
        Now.AddDays(-5),
        tier);

    /// <summary>
    /// Builds a gallery with 15 included photos and ₡3 000 per extra photo.
    /// </summary>
    /// <param name="status">The state.</param>
    /// <param name="photographer">The photographer; a new one when omitted.</param>
    /// <returns>The gallery.</returns>
    private static Gallery NewGallery(GalleryStatus status, Guid? photographer = null) => Gallery.Restore(
        Guid.CreateVersion7(),
        1,
        photographer ?? Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        new GallerySnapshot("María Pérez", "Retrato Familiar", Now.AddDays(-1)),
        status,
        15,
        0,
        Money.Create(3_000m, "CRC"),
        Now,
        Now);
}
