using MongoDB.Driver;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Pagination;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;
using PhotoStudio.Infrastructure.Persistence;
using PhotoStudio.Infrastructure.Persistence.Documents;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Tests of <see cref="MongoGalleryRepository"/> against a real MongoDB: one gallery per booking, optimistic concurrency and
/// the pages of the list.
/// </summary>
/// <param name="fixture">Throwaway database shared by the tests of this class.</param>
public sealed class MongoGalleryRepositoryTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A gallery survives being saved and read, by identifier and by booking, with everything it holds.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TryAddAsync_ThenRead_ReturnsTheSameGallery()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var gallery = NewGallery(Guid.CreateVersion7(), Now);

        (await repository.TryAddAsync(gallery, TestContext.Current.CancellationToken)).ShouldBeTrue();
        var byId = await repository.GetByIdAsync(gallery.Id, TestContext.Current.CancellationToken);
        var byBooking = await repository.GetByBookingIdAsync(gallery.BookingId, TestContext.Current.CancellationToken);

        byId.ShouldNotBeNull();
        byBooking.ShouldNotBeNull();
        byBooking.Id.ShouldBe(gallery.Id);
        byId.Version.ShouldBe(1);
        byId.PhotographerId.ShouldBe(gallery.PhotographerId);
        byId.BookingId.ShouldBe(gallery.BookingId);
        byId.Snapshot.ShouldBe(gallery.Snapshot);
        byId.Status.ShouldBe(GalleryStatus.Pending);
        byId.IncludedPhotos.ShouldBe(30);
        byId.BonusPhotos.ShouldBe(0);
        byId.ExtraPhotoPrice.ShouldBe(Money.Create(3_000m, "CRC"));
        byId.CreatedAt.ShouldBe(Now);
    }

    /// <summary>
    /// Asking for a gallery that does not exist answers null, not an error.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Read_OfAMissingGallery_ReturnsNull()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();

        (await repository.GetByIdAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken)).ShouldBeNull();
        (await repository.GetByBookingIdAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>
    /// A booking has one gallery: adding a second one for it is refused without an error, and the first one stays.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TryAddAsync_ForABookingThatHasOne_ReturnsFalseAndKeepsTheFirst()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var booking = Guid.CreateVersion7();
        var first = NewGallery(Guid.CreateVersion7(), Now, booking);
        var second = NewGallery(Guid.CreateVersion7(), Now.AddMinutes(1), booking);

        (await repository.TryAddAsync(first, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await repository.TryAddAsync(second, TestContext.Current.CancellationToken)).ShouldBeFalse();

        (await repository.GetByBookingIdAsync(booking, TestContext.Current.CancellationToken))!.Id.ShouldBe(first.Id);
    }

    /// <summary>
    /// Twenty deliveries of the same event at once create exactly one gallery: the unique index decides, not a check first.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TryAddAsync_InParallel_CreatesExactlyOne()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var booking = Guid.CreateVersion7();

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            repository.TryAddAsync(NewGallery(Guid.CreateVersion7(), Now, booking), TestContext.Current.CancellationToken)));

        results.Count(added => added).ShouldBe(1);
    }

    /// <summary>
    /// Saving changes the gallery and raises its version; saving a stale copy is a conflict and changes nothing.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UpdateAsync_SavesTheChange_AndRefusesAStaleCopy()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var gallery = NewGallery(Guid.CreateVersion7(), Now);
        await repository.TryAddAsync(gallery, TestContext.Current.CancellationToken);
        var firstCopy = (await repository.GetByIdAsync(gallery.Id, TestContext.Current.CancellationToken))!;
        var staleCopy = (await repository.GetByIdAsync(gallery.Id, TestContext.Current.CancellationToken))!;

        firstCopy.EditTerms(50, 5, Money.Create(2_000m, "CRC"), Now.AddHours(1));
        await repository.UpdateAsync(firstCopy, TestContext.Current.CancellationToken);
        staleCopy.EditTerms(10, 0, Money.Create(9_000m, "CRC"), Now.AddHours(2));
        var error = await Should.ThrowAsync<ConflictException>(() => repository.UpdateAsync(staleCopy, TestContext.Current.CancellationToken));

        error.Code.ShouldBe(ApplicationErrorCodes.ConcurrencyConflict);
        var stored = (await repository.GetByIdAsync(gallery.Id, TestContext.Current.CancellationToken))!;
        stored.Version.ShouldBe(2);
        stored.IncludedPhotos.ShouldBe(50);
        stored.BonusPhotos.ShouldBe(5);
        stored.ExtraPhotoPrice.Amount.ShouldBe(2_000m);
        stored.UpdatedAt.ShouldBe(Now.AddHours(1));
    }

    /// <summary>
    /// The list gives only the photographer's galleries, the most recently created first.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ListByPhotographerAsync_ReturnsOnlyOwnGalleriesNewestFirst()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var photographer = Guid.CreateVersion7();
        var older = NewGallery(photographer, Now.AddMinutes(-5));
        var newer = NewGallery(photographer, Now);
        var foreign = NewGallery(Guid.CreateVersion7(), Now);
        foreach (var gallery in new[] { older, newer, foreign })
        {
            await repository.TryAddAsync(gallery, TestContext.Current.CancellationToken);
        }

        var listed = await repository.ListByPhotographerAsync(photographer, null, 50, TestContext.Current.CancellationToken);

        listed.Select(gallery => gallery.Id).ShouldBe([newer.Id, older.Id]);
    }

    /// <summary>
    /// A cursor continues the list right after the gallery it names, with no repeats and no gaps, even when several galleries
    /// were created at the same instant (the identifier breaks the tie).
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ListByPhotographerAsync_WithACursor_ContinuesRightAfterIt()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var photographer = Guid.CreateVersion7();
        var sameInstant = Enumerable.Range(0, 3).Select(_ => NewGallery(photographer, Now)).ToList();
        var older = NewGallery(photographer, Now.AddMinutes(-1));
        foreach (var gallery in sameInstant.Append(older))
        {
            await repository.TryAddAsync(gallery, TestContext.Current.CancellationToken);
        }

        var first = await repository.ListByPhotographerAsync(photographer, null, 2, TestContext.Current.CancellationToken);
        var second = await repository.ListByPhotographerAsync(photographer, new PageCursor(first[^1].CreatedAt, first[^1].Id), 2, TestContext.Current.CancellationToken);

        var expected = sameInstant.OrderByDescending(gallery => gallery.Id).Select(gallery => gallery.Id).Append(older.Id).ToList();
        first.Concat(second).Select(gallery => gallery.Id).ShouldBe(expected);
    }

    /// <summary>
    /// The indexes exist after startup: one gallery per booking (unique) and the one that serves the list.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Startup_CreatesTheGalleryIndexes()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await NewRepositoryAsync();

        var indexes = await (await fixture.Database.GetCollection<GalleryDocument>(MongoGalleryRepository.CollectionName).Indexes.ListAsync(TestContext.Current.CancellationToken))
            .ToListAsync(TestContext.Current.CancellationToken);

        indexes.ShouldContain(index => index["name"] == "ix_gallery_booking" && index.GetValue("unique", false).AsBoolean);
        indexes.ShouldContain(index => index["name"] == "ix_gallery_photographer_created");
    }

    /// <summary>
    /// Creates the repository after making sure the indexes exist.
    /// </summary>
    /// <returns>The repository.</returns>
    private async Task<MongoGalleryRepository> NewRepositoryAsync()
    {
        await new MongoIndexInitializer(fixture.Database, Microsoft.Extensions.Logging.Abstractions.NullLogger<MongoIndexInitializer>.Instance)
            .StartAsync(TestContext.Current.CancellationToken);
        return new MongoGalleryRepository(fixture.Database);
    }

    /// <summary>
    /// Builds a gallery with 30 included photos and ₡3 000 per extra photo.
    /// </summary>
    /// <param name="photographer">The photographer.</param>
    /// <param name="createdAt">Creation instant.</param>
    /// <param name="booking">The booking; a new one when omitted.</param>
    /// <returns>The gallery.</returns>
    private static Gallery NewGallery(Guid photographer, DateTimeOffset createdAt, Guid? booking = null) => Gallery.Create(
        photographer,
        booking ?? Guid.CreateVersion7(),
        new GallerySnapshot("María Pérez", "Retrato Familiar", createdAt.AddDays(-1)),
        30,
        Money.Create(3_000m, "CRC"),
        createdAt);
}
