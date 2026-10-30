using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Domain.UnitTests.Galleries;

/// <summary>
/// Tests of the <see cref="Gallery"/> aggregate: how it is created and how its terms change.
/// </summary>
public sealed class GalleryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly GallerySnapshot Snapshot = new("María Pérez", "Retrato Familiar", Now.AddDays(-1));

    /// <summary>
    /// A new gallery waits for the proofs, with the included photos and the price it was given, and no gift.
    /// </summary>
    [Fact]
    public void Create_StartsPendingWithTheGivenTerms()
    {
        var photographer = Guid.CreateVersion7();
        var booking = Guid.CreateVersion7();

        var gallery = Gallery.Create(photographer, booking, Snapshot, 30, Money.Create(3_000m, "CRC"), Now);

        gallery.Id.ShouldNotBe(Guid.Empty);
        gallery.Version.ShouldBe(0);
        gallery.PhotographerId.ShouldBe(photographer);
        gallery.BookingId.ShouldBe(booking);
        gallery.Snapshot.ShouldBe(Snapshot);
        gallery.Status.ShouldBe(GalleryStatus.Pending);
        gallery.IncludedPhotos.ShouldBe(30);
        gallery.BonusPhotos.ShouldBe(0);
        gallery.SelectablePhotos.ShouldBe(30);
        gallery.ExtraPhotoPrice.ShouldBe(Money.Create(3_000m, "CRC"));
        gallery.CreatedAt.ShouldBe(Now);
        gallery.UpdatedAt.ShouldBe(Now);
        gallery.DomainEvents.ShouldBeEmpty();
    }

    /// <summary>
    /// A gallery cannot exist without its photographer and its booking.
    /// </summary>
    /// <param name="withPhotographer">Whether the photographer is given.</param>
    /// <param name="withBooking">Whether the booking is given.</param>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Create_WithoutAnIdentifier_IsRefused(bool withPhotographer, bool withBooking)
    {
        var error = Should.Throw<DomainException>(() => Gallery.Create(
            withPhotographer ? Guid.CreateVersion7() : Guid.Empty,
            withBooking ? Guid.CreateVersion7() : Guid.Empty,
            Snapshot,
            15,
            Money.Create(3_000m, "CRC"),
            Now));

        error.Code.ShouldBe(DomainErrorCodes.RequiredValue);
    }

    /// <summary>
    /// The included photos have bounds: none negative, and no more than a gallery holds.
    /// </summary>
    /// <param name="included">The included photos.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(Gallery.MaxPhotos + 1)]
    public void Create_WithInvalidIncludedPhotos_IsRefused(int included) =>
        Should.Throw<DomainException>(() => Gallery.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Snapshot, included, Money.Create(3_000m, "CRC"), Now))
            .Code.ShouldBe(DomainErrorCodes.GalleryInvalidTerms);

    /// <summary>
    /// Zero included photos is valid: a custom package gets its photos from the photographer later.
    /// </summary>
    [Fact]
    public void Create_WithNoIncludedPhotos_IsValid() =>
        Gallery.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Snapshot, 0, Money.Create(3_000m, "CRC"), Now).SelectablePhotos.ShouldBe(0);

    /// <summary>
    /// The photographer can change the three terms while the client has not sent the selection, and the gift counts toward
    /// the photos the client may pick.
    /// </summary>
    /// <param name="status">A state in which the terms can still change.</param>
    [Theory]
    [InlineData(GalleryStatus.Pending)]
    [InlineData(GalleryStatus.InSelection)]
    public void EditTerms_WhileTheSelectionIsNotSent_ChangesTheTerms(GalleryStatus status)
    {
        var gallery = InState(status);
        var later = Now.AddHours(2);

        gallery.EditTerms(30, 5, Money.Create(2_500m, "CRC"), later);

        gallery.IncludedPhotos.ShouldBe(30);
        gallery.BonusPhotos.ShouldBe(5);
        gallery.SelectablePhotos.ShouldBe(35);
        gallery.ExtraPhotoPrice.Amount.ShouldBe(2_500m);
        gallery.UpdatedAt.ShouldBe(later);
        gallery.CreatedAt.ShouldBe(Now);
        gallery.Status.ShouldBe(status);
    }

    /// <summary>
    /// Once the client sent the selection, the terms are frozen: the selection and the charge for the extras were calculated with them.
    /// </summary>
    /// <param name="status">A state from which the terms cannot change.</param>
    [Theory]
    [InlineData(GalleryStatus.InEditing)]
    [InlineData(GalleryStatus.Ready)]
    [InlineData(GalleryStatus.Delivered)]
    [InlineData(GalleryStatus.Archived)]
    public void EditTerms_AfterTheSelectionWasSent_IsRefused(GalleryStatus status)
    {
        var gallery = InState(status);

        var error = Should.Throw<DomainException>(() => gallery.EditTerms(30, 5, Money.Create(2_500m, "CRC"), Now.AddHours(1)));

        error.Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
        gallery.IncludedPhotos.ShouldBe(15);
        gallery.UpdatedAt.ShouldBe(Now);
    }

    /// <summary>
    /// The counts must make sense, and a refused change leaves the gallery as it was.
    /// </summary>
    /// <param name="included">Included photos.</param>
    /// <param name="bonus">Gift photos.</param>
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(15, -1)]
    [InlineData(100, 51)]
    [InlineData(Gallery.MaxPhotos + 1, 0)]
    public void EditTerms_WithInvalidCounts_IsRefusedAndChangesNothing(int included, int bonus)
    {
        var gallery = InState(GalleryStatus.Pending);

        Should.Throw<DomainException>(() => gallery.EditTerms(included, bonus, Money.Create(2_500m, "CRC"), Now.AddHours(1)))
            .Code.ShouldBe(DomainErrorCodes.GalleryInvalidTerms);

        gallery.IncludedPhotos.ShouldBe(15);
        gallery.BonusPhotos.ShouldBe(0);
        gallery.ExtraPhotoPrice.Amount.ShouldBe(3_000m);
        gallery.UpdatedAt.ShouldBe(Now);
    }

    /// <summary>
    /// The included photos and the gift can add up to exactly what a gallery holds.
    /// </summary>
    [Fact]
    public void EditTerms_UpToTheMaximum_IsValid()
    {
        var gallery = InState(GalleryStatus.Pending);

        gallery.EditTerms(100, 50, Money.Create(0m, "CRC"), Now);

        gallery.SelectablePhotos.ShouldBe(Gallery.MaxPhotos);
        gallery.ExtraPhotoPrice.Amount.ShouldBe(0m);
    }

    /// <summary>
    /// The price keeps the currency of the booking: it cannot be changed into another one by editing the terms.
    /// </summary>
    [Fact]
    public void EditTerms_WithAnotherCurrency_IsRefused()
    {
        var gallery = InState(GalleryStatus.Pending);

        Should.Throw<DomainException>(() => gallery.EditTerms(15, 0, Money.Create(5m, "USD"), Now.AddHours(1)))
            .Code.ShouldBe(DomainErrorCodes.CurrencyMismatch);

        gallery.ExtraPhotoPrice.Currency.ShouldBe("CRC");
    }

    /// <summary>
    /// Editing the terms is the only action for now, and only while the selection is not sent.
    /// </summary>
    /// <param name="status">The state.</param>
    /// <param name="allowed">Whether the terms can be edited in it.</param>
    [Theory]
    [InlineData(GalleryStatus.Pending, true)]
    [InlineData(GalleryStatus.InSelection, true)]
    [InlineData(GalleryStatus.InEditing, false)]
    [InlineData(GalleryStatus.Ready, false)]
    [InlineData(GalleryStatus.Delivered, false)]
    [InlineData(GalleryStatus.Archived, false)]
    public void GetAllowedActions_FollowsTheState(GalleryStatus status, bool allowed) =>
        InState(status).GetAllowedActions().Contains(GalleryAction.EditTerms).ShouldBe(allowed);

    /// <summary>
    /// Restoring keeps everything that was stored, including the version.
    /// </summary>
    [Fact]
    public void Restore_KeepsWhatWasStored()
    {
        var id = Guid.CreateVersion7();

        var gallery = Gallery.Restore(
            id, 7, Guid.CreateVersion7(), Guid.CreateVersion7(), Snapshot, GalleryStatus.InSelection, 30, 5, Money.Create(3_000m, "CRC"), Now, Now.AddDays(1));

        gallery.Id.ShouldBe(id);
        gallery.Version.ShouldBe(7);
        gallery.Status.ShouldBe(GalleryStatus.InSelection);
        gallery.SelectablePhotos.ShouldBe(35);
        gallery.UpdatedAt.ShouldBe(Now.AddDays(1));
    }

    /// <summary>
    /// Builds a gallery in a given state, with 15 included photos and ₡3 000 per extra photo.
    /// </summary>
    /// <param name="status">The state.</param>
    /// <returns>The gallery.</returns>
    private static Gallery InState(GalleryStatus status) =>
        Gallery.Restore(Guid.CreateVersion7(), 1, Guid.CreateVersion7(), Guid.CreateVersion7(), Snapshot, status, 15, 0, Money.Create(3_000m, "CRC"), Now, Now);
}
