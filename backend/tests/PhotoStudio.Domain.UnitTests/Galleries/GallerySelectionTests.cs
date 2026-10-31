using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;
using PhotoStudio.Domain.Galleries.Events;

namespace PhotoStudio.Domain.UnitTests.Galleries;

/// <summary>
/// Tests of the selection a client sends to a <see cref="Gallery"/> (G3).
/// </summary>
public sealed class GallerySelectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Sending the selection locks it: the gallery goes to editing, remembers what was chosen and when, and raises the event.
    /// </summary>
    [Fact]
    public void SubmitSelection_RecordsTheChoiceAndMovesToEditing()
    {
        var gallery = OpenGallery(photos: 5, included: 3);
        var chosen = gallery.Photos.Take(2).Select(photo => photo.Id).ToList();

        gallery.SubmitSelection(chosen, Now.AddHours(1)).ShouldBeTrue();

        gallery.Status.ShouldBe(GalleryStatus.InEditing);
        gallery.SelectedPhotoIds.ShouldBe(chosen);
        gallery.SelectionSubmittedAt.ShouldBe(Now.AddHours(1));
        gallery.UpdatedAt.ShouldBe(Now.AddHours(1));
        gallery.ExtraPhotoCount.ShouldBe(0);
        var raised = gallery.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<SelectionSubmitted>();
        raised.SelectedPhotos.ShouldBe(2);
        raised.ExtraPhotos.ShouldBe(0);
        raised.GalleryId.ShouldBe(gallery.Id);
    }

    /// <summary>
    /// The photos beyond what the gallery includes, counting the gift ones, are the extra ones, at the price frozen in the gallery.
    /// </summary>
    [Fact]
    public void SubmitSelection_BeyondTheIncludedPhotos_CountsAndPricesTheExtraOnes()
    {
        var gallery = OpenGallery(photos: 8, included: 3, bonus: 1);

        gallery.SubmitSelection([.. gallery.Photos.Take(6).Select(photo => photo.Id)], Now);

        gallery.ExtraPhotoCount.ShouldBe(2);
        gallery.ExtraPhotoTotal.ShouldBe(Money.Create(6_000m, "CRC"));
        var raised = gallery.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<SelectionSubmitted>();
        raised.ExtraPhotos.ShouldBe(2);
        raised.ExtraTotal.ShouldBe(6_000m);
        raised.Currency.ShouldBe("CRC");
    }

    /// <summary>
    /// Choosing exactly what is included costs nothing extra.
    /// </summary>
    [Fact]
    public void SubmitSelection_OfExactlyTheIncludedPhotos_HasNoExtra()
    {
        var gallery = OpenGallery(photos: 5, included: 3);

        gallery.SubmitSelection([.. gallery.Photos.Take(3).Select(photo => photo.Id)], Now);

        gallery.ExtraPhotoCount.ShouldBe(0);
        gallery.ExtraPhotoTotal.Amount.ShouldBe(0m);
    }

    /// <summary>
    /// Sending the very same selection again does nothing: a double click or a retry is safe and raises no second event.
    /// </summary>
    [Fact]
    public void SubmitSelection_TheSameSelectionTwice_IsRecordedOnce()
    {
        var gallery = OpenGallery(photos: 4, included: 2);
        var chosen = gallery.Photos.Take(2).Select(photo => photo.Id).ToList();
        gallery.SubmitSelection(chosen, Now);

        gallery.SubmitSelection([.. chosen.AsEnumerable().Reverse()], Now.AddMinutes(1)).ShouldBeFalse();

        gallery.SelectionSubmittedAt.ShouldBe(Now);
        gallery.DomainEvents.Count.ShouldBe(1);
    }

    /// <summary>
    /// Once sent, a different selection is refused: only the photographer can reopen it.
    /// </summary>
    [Fact]
    public void SubmitSelection_ADifferentOneAfterSending_IsRefused()
    {
        var gallery = OpenGallery(photos: 4, included: 2);
        gallery.SubmitSelection([gallery.Photos[0].Id], Now);

        Should.Throw<DomainException>(() => gallery.SubmitSelection([gallery.Photos[1].Id], Now))
            .Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);

        gallery.SelectedPhotoIds.ShouldBe([gallery.Photos[0].Id]);
    }

    /// <summary>
    /// A gallery whose selection is not open cannot take a selection.
    /// </summary>
    /// <param name="status">State of the gallery.</param>
    [Theory]
    [InlineData(GalleryStatus.Pending)]
    [InlineData(GalleryStatus.InEditing)]
    [InlineData(GalleryStatus.Ready)]
    [InlineData(GalleryStatus.Delivered)]
    [InlineData(GalleryStatus.Archived)]
    public void SubmitSelection_WhenTheSelectionIsNotOpen_IsRefused(GalleryStatus status)
    {
        var gallery = OpenGallery(photos: 3, included: 2, status: status);

        Should.Throw<DomainException>(() => gallery.SubmitSelection([gallery.Photos[0].Id], Now))
            .Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
    }

    /// <summary>
    /// A selection with no photos, a repeated photo, or a photo that is not in the gallery is refused and changes nothing.
    /// </summary>
    /// <param name="scenario">What is wrong with the selection.</param>
    [Theory]
    [InlineData("none")]
    [InlineData("repeated")]
    [InlineData("foreign")]
    public void SubmitSelection_WithInvalidPhotos_IsRefused(string scenario)
    {
        var gallery = OpenGallery(photos: 3, included: 2);
        Guid[] photoIds = scenario switch
        {
            "none" => [],
            "repeated" => [gallery.Photos[0].Id, gallery.Photos[0].Id],
            _ => [gallery.Photos[0].Id, Guid.CreateVersion7()],
        };

        Should.Throw<DomainException>(() => gallery.SubmitSelection(photoIds, Now))
            .Code.ShouldBe(DomainErrorCodes.GalleryInvalidSelection);

        gallery.Status.ShouldBe(GalleryStatus.InSelection);
        gallery.SelectedPhotoIds.ShouldBeEmpty();
        gallery.DomainEvents.ShouldBeEmpty();
    }

    /// <summary>
    /// After the client sends the selection the photographer can no longer edit the terms it was priced with.
    /// </summary>
    [Fact]
    public void SubmitSelection_LocksTheTerms()
    {
        var gallery = OpenGallery(photos: 3, included: 2);
        gallery.SubmitSelection([gallery.Photos[0].Id], Now);

        gallery.GetAllowedActions().ShouldBeEmpty();
        Should.Throw<DomainException>(() => gallery.EditTerms(5, 0, Money.Create(3_000m, "CRC"), Now))
            .Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
    }

    /// <summary>
    /// A restored gallery keeps the selection, its order and the instant it was sent.
    /// </summary>
    [Fact]
    public void Restore_KeepsTheSelection()
    {
        var photos = Enumerable.Range(0, 3).Select(index => new GalleryPhoto(Guid.CreateVersion7(), $"{index}.jpg", 10, Now)).ToList();
        var chosen = new[] { photos[2].Id, photos[0].Id };

        var gallery = Gallery.Restore(
            Guid.CreateVersion7(), 4, Guid.CreateVersion7(), Guid.CreateVersion7(), new GallerySnapshot("María", "Retrato", Now), GalleryStatus.InEditing,
            1, 0, Money.Create(3_000m, "CRC"), Now, Now, photos, null, chosen, Now.AddHours(2));

        gallery.SelectedPhotoIds.ShouldBe(chosen);
        gallery.SelectionSubmittedAt.ShouldBe(Now.AddHours(2));
        gallery.ExtraPhotoCount.ShouldBe(1);
    }

    /// <summary>
    /// Builds a gallery whose selection is open (or in another state), with proofs.
    /// </summary>
    /// <param name="photos">Number of proofs.</param>
    /// <param name="included">Included photos.</param>
    /// <param name="bonus">Gift photos.</param>
    /// <param name="status">State of the gallery.</param>
    /// <returns>The gallery.</returns>
    private static Gallery OpenGallery(int photos, int included, int bonus = 0, GalleryStatus status = GalleryStatus.InSelection) => Gallery.Restore(
        Guid.CreateVersion7(),
        1,
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        new GallerySnapshot("María Pérez", "Retrato Familiar", Now.AddDays(-1)),
        status,
        included,
        bonus,
        Money.Create(3_000m, "CRC"),
        Now,
        Now,
        Enumerable.Range(0, photos).Select(index => new GalleryPhoto(Guid.CreateVersion7(), $"IMG_{index:0000}.jpg", 10, Now)));
}
