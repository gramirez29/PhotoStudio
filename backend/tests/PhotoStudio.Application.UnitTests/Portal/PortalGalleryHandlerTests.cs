using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Galleries.Selection;
using PhotoStudio.Application.Portal.Galleries;
using PhotoStudio.Application.Storage;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;
using PhotoStudio.Domain.Galleries.Events;

namespace PhotoStudio.Application.UnitTests.Portal;

/// <summary>
/// Tests of what the client sees of their gallery and of sending the selection: the summary that only needs the link, the gallery
/// that needs the session, and the selection that locks it.
/// </summary>
public sealed class PortalGalleryHandlerTests
{
    private readonly PortalFixture _fixture = new();
    private readonly IGalleryRepository _galleries = Substitute.For<IGalleryRepository>();
    private readonly IObjectStorage _storage = Substitute.For<IObjectStorage>();

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalGalleryHandlerTests"/> class: every address the storage signs is a fresh one.
    /// </summary>
    public PortalGalleryHandlerTests() =>
        _storage.CreateDownloadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => new PresignedUrl(new Uri($"https://storage.test/{call.ArgAt<string>(0)}?sig=secret"), PortalFixture.Now.Add(call.ArgAt<TimeSpan>(1)), new Dictionary<string, string>()));

    /// <summary>
    /// With only the link the client learns where the gallery is, and nothing about the photos.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Summary_OfAnOpenGallery_SaysWhereItIs()
    {
        MakeGallery(GalleryStatus.InSelection, 3);

        var summary = await new GetPortalGallerySummaryHandler(_galleries, _fixture.Links, _fixture.Tokens, _fixture.Clock)
            .HandleAsync(new GetPortalGallerySummaryQuery(PortalFixture.Token), TestContext.Current.CancellationToken);

        summary.Status.ShouldBe(GalleryStatus.InSelection);
    }

    /// <summary>
    /// A booking without a gallery, a gallery still waiting for its proofs and a gallery of another photographer are the same
    /// "not found": the client cannot tell them apart.
    /// </summary>
    /// <param name="scenario">What is the matter with the gallery.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("none")]
    [InlineData("pending")]
    [InlineData("other-photographer")]
    public async Task Summary_WhenThereIsNothingToSee_IsNotFound(string scenario)
    {
        switch (scenario)
        {
            case "pending":
                MakeGallery(GalleryStatus.Pending, 2);
                break;
            case "other-photographer":
                MakeGallery(GalleryStatus.InSelection, 2, photographerId: Guid.CreateVersion7());
                break;
            default:
                break;
        }

        await Should.ThrowAsync<NotFoundException>(() => new GetPortalGallerySummaryHandler(_galleries, _fixture.Links, _fixture.Tokens, _fixture.Clock)
            .HandleAsync(new GetPortalGallerySummaryQuery(PortalFixture.Token), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// With the session the client gets every proof with a signed address that lasts twenty minutes, the terms to choose, no file
    /// name, and no selection while they have not sent one.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Get_GivesTheProofsAndTheTerms()
    {
        var gallery = MakeGallery(GalleryStatus.InSelection, 3, included: 2, bonus: 1);

        var response = await GetHandler().HandleAsync(new GetPortalGalleryQuery(PortalFixture.Token, _fixture.Booking.Id), TestContext.Current.CancellationToken);

        response.Status.ShouldBe(GalleryStatus.InSelection);
        response.SelectablePhotos.ShouldBe(3);
        response.ExtraPhotoPrice.Amount.ShouldBe(3_000m);
        response.Photos.Select(photo => photo.Id).ShouldBe(gallery.Photos.Select(photo => photo.Id));
        response.Photos.ShouldAllBe(photo => photo.Url.ToString().Contains($"galleries/{gallery.Id}/proofs/{photo.Id}"));
        response.Photos.ShouldAllBe(photo => photo.ExpiresAt == PortalFixture.Now.AddMinutes(20));
        response.Selection.ShouldBeNull();
        await _storage.Received(3).CreateDownloadUrlAsync(Arg.Any<string>(), TimeSpan.FromMinutes(20), Arg.Is<string?>(name => name == null), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A session issued for another booking cannot read this gallery, even with a valid link token.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Get_WithASessionOfAnotherBooking_IsNotFound()
    {
        MakeGallery(GalleryStatus.InSelection, 2);

        await Should.ThrowAsync<NotFoundException>(() => GetHandler().HandleAsync(new GetPortalGalleryQuery(PortalFixture.Token, Guid.CreateVersion7()), TestContext.Current.CancellationToken));

        await _storage.DidNotReceive().CreateDownloadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Sending the selection records it, saves the gallery once and answers with the selection and what the extra photos cost.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Submit_RecordsTheSelectionAndSavesTheGallery()
    {
        var gallery = MakeGallery(GalleryStatus.InSelection, 4, included: 2);
        var chosen = gallery.Photos.Take(3).Select(photo => photo.Id).ToList();

        var response = await SubmitHandler().HandleAsync(new SubmitPortalSelectionCommand(PortalFixture.Token, _fixture.Booking.Id, chosen), TestContext.Current.CancellationToken);

        response.Status.ShouldBe(GalleryStatus.InEditing);
        response.Selection.ShouldNotBeNull().PhotoIds.ShouldBe(chosen);
        response.Selection.ExtraPhotos.ShouldBe(1);
        response.Selection.ExtraTotal.Amount.ShouldBe(3_000m);
        gallery.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<SelectionSubmitted>();
        await _galleries.Received(1).UpdateAsync(gallery, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Sending the same selection again answers the same way and saves nothing, so a double click does not write twice.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Submit_TheSameSelectionTwice_SavesOnce()
    {
        var gallery = MakeGallery(GalleryStatus.InSelection, 3, included: 3);
        var chosen = gallery.Photos.Take(2).Select(photo => photo.Id).ToList();
        var command = new SubmitPortalSelectionCommand(PortalFixture.Token, _fixture.Booking.Id, chosen);

        await SubmitHandler().HandleAsync(command, TestContext.Current.CancellationToken);
        var second = await SubmitHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        second.Selection.ShouldNotBeNull().PhotoIds.ShouldBe(chosen);
        await _galleries.Received(1).UpdateAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A session of another booking cannot send a selection, and nothing is saved.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Submit_WithASessionOfAnotherBooking_IsNotFound()
    {
        var gallery = MakeGallery(GalleryStatus.InSelection, 2);

        await Should.ThrowAsync<NotFoundException>(() => SubmitHandler().HandleAsync(
            new SubmitPortalSelectionCommand(PortalFixture.Token, Guid.CreateVersion7(), [gallery.Photos[0].Id]), TestContext.Current.CancellationToken));

        await _galleries.DidNotReceive().UpdateAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A selection with a photo that is not in the gallery, with no photos or with more than a gallery can hold is refused and not saved.
    /// </summary>
    /// <param name="scenario">What is wrong with the selection.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("foreign")]
    [InlineData("none")]
    [InlineData("too-many")]
    public async Task Submit_WithInvalidPhotos_IsRefused(string scenario)
    {
        var gallery = MakeGallery(GalleryStatus.InSelection, 2);
        IReadOnlyList<Guid> photoIds = scenario switch
        {
            "foreign" => [gallery.Photos[0].Id, Guid.CreateVersion7()],
            "none" => [],
            _ => [.. Enumerable.Range(0, 151).Select(_ => Guid.CreateVersion7())],
        };

        var error = await Should.ThrowAsync<DomainException>(() => SubmitHandler().HandleAsync(
            new SubmitPortalSelectionCommand(PortalFixture.Token, _fixture.Booking.Id, photoIds), TestContext.Current.CancellationToken));

        error.Code.ShouldBe(DomainErrorCodes.GalleryInvalidSelection);
        await _galleries.DidNotReceive().UpdateAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The photographer sees the photos the client chose with the names of their original files, in the order they were chosen.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task PhotographerSelection_ListsTheNamesInTheOrderChosen()
    {
        var gallery = MakeGallery(GalleryStatus.InSelection, 3, included: 1);
        gallery.SubmitSelection([gallery.Photos[2].Id, gallery.Photos[0].Id], PortalFixture.Now);

        var response = await new GetSelectionHandler(_galleries)
            .HandleAsync(new GetSelectionQuery(gallery.PhotographerId, gallery.Id), TestContext.Current.CancellationToken);

        response.SubmittedAt.ShouldBe(PortalFixture.Now);
        response.ExtraPhotos.ShouldBe(1);
        response.Photos.Select(photo => photo.Name).ShouldBe(["IMG_0002.jpg", "IMG_0000.jpg"]);
    }

    /// <summary>
    /// Before the client sends it the selection is empty, and a gallery of another photographer is not found.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task PhotographerSelection_BeforeItIsSent_IsEmpty_AndOfAnotherPhotographerIsNotFound()
    {
        var gallery = MakeGallery(GalleryStatus.InSelection, 2);
        var handler = new GetSelectionHandler(_galleries);

        var response = await handler.HandleAsync(new GetSelectionQuery(gallery.PhotographerId, gallery.Id), TestContext.Current.CancellationToken);

        response.SubmittedAt.ShouldBeNull();
        response.Photos.ShouldBeEmpty();
        await Should.ThrowAsync<NotFoundException>(() => handler.HandleAsync(new GetSelectionQuery(Guid.CreateVersion7(), gallery.Id), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Creates the handler that gives the gallery to the client.
    /// </summary>
    /// <returns>The handler.</returns>
    private GetPortalGalleryHandler GetHandler() => new(_galleries, _fixture.Links, _fixture.Tokens, _storage, _fixture.Clock);

    /// <summary>
    /// Creates the handler that sends the selection.
    /// </summary>
    /// <returns>The handler.</returns>
    private SubmitPortalSelectionHandler SubmitHandler() => new(_galleries, _fixture.Links, _fixture.Tokens, _storage, _fixture.Clock);

    /// <summary>
    /// Makes the repository return a gallery of the booking of the fixture.
    /// </summary>
    /// <param name="status">State of the gallery.</param>
    /// <param name="photos">Number of proofs.</param>
    /// <param name="included">Included photos.</param>
    /// <param name="bonus">Gift photos.</param>
    /// <param name="photographerId">Photographer the gallery belongs to; the one of the booking by default.</param>
    /// <returns>The gallery.</returns>
    private Gallery MakeGallery(GalleryStatus status, int photos, int included = 2, int bonus = 0, Guid? photographerId = null)
    {
        var gallery = Domain.Galleries.Gallery.Restore(
            Guid.CreateVersion7(),
            1,
            photographerId ?? _fixture.Booking.PhotographerId,
            _fixture.Booking.Id,
            new GallerySnapshot("María Pérez", "Retrato familiar", PortalFixture.Now),
            status,
            included,
            bonus,
            Money.Create(3_000m, "CRC"),
            PortalFixture.Now,
            PortalFixture.Now,
            Enumerable.Range(0, photos).Select(index => new GalleryPhoto(Guid.CreateVersion7(), $"IMG_{index:0000}.jpg", 10, PortalFixture.Now)));
        _galleries.GetByBookingIdAsync(_fixture.Booking.Id, Arg.Any<CancellationToken>()).Returns(gallery);
        _galleries.GetByIdAsync(gallery.Id, Arg.Any<CancellationToken>()).Returns(gallery);
        return gallery;
    }
}
