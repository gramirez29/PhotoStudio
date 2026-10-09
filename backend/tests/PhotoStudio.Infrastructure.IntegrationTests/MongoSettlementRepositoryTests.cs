using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Billing;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using PhotoStudio.Infrastructure.Persistence;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Tests of <see cref="MongoSettlementRepository"/> against a real MongoDB replica set.
/// </summary>
/// <param name="fixture">Throwaway database shared by the tests of this class.</param>
public sealed class MongoSettlementRepositoryTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Builds a settlement with a pending refund.
    /// </summary>
    /// <param name="photographerId">Owner.</param>
    /// <param name="bookingId">Booking.</param>
    /// <param name="createdAt">Instant it is opened.</param>
    /// <returns>The settlement.</returns>
    private static BookingSettlement Pending(Guid photographerId, Guid bookingId, DateTimeOffset createdAt) => BookingSettlement.Open(
        photographerId,
        bookingId,
        new SettlementSnapshot("María Pérez", "+50688888888", "Retrato", Now.AddDays(5)),
        new SettlementOutcome(SettlementReason.ClientCancelledLate, Money.Create(80_000m), Money.Create(50_000m), Money.Create(30_000m)),
        createdAt);

    /// <summary>
    /// Creates the repository after making sure the indexes exist.
    /// </summary>
    /// <returns>The repository.</returns>
    private async Task<MongoSettlementRepository> NewRepositoryAsync()
    {
        await new MongoIndexInitializer(fixture.Database, NullLogger<MongoIndexInitializer>.Instance).StartAsync(TestContext.Current.CancellationToken);
        return new MongoSettlementRepository(fixture.Database);
    }

    /// <summary>
    /// A settlement survives a round trip with its amounts, states and snapshot.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TryAddAsync_ThenGet_RoundTripsTheSettlement()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var settlement = Pending(Guid.CreateVersion7(), Guid.CreateVersion7(), Now);

        (await repository.TryAddAsync(settlement, TestContext.Current.CancellationToken)).ShouldBeTrue();
        var stored = (await repository.GetByBookingIdAsync(settlement.BookingId, TestContext.Current.CancellationToken))!;

        stored.Id.ShouldBe(settlement.Id);
        stored.Reason.ShouldBe(SettlementReason.ClientCancelledLate);
        stored.TotalPaid.ShouldBe(Money.Create(80_000m));
        stored.Retained.ShouldBe(Money.Create(50_000m));
        stored.RetentionStatus.ShouldBe(RetentionStatus.Applied);
        stored.RefundAmount.ShouldBe(Money.Create(30_000m));
        stored.RefundStatus.ShouldBe(RefundStatus.Pending);
        stored.Snapshot.ClientName.ShouldBe("María Pérez");
        stored.Snapshot.SessionStart.ShouldBe(Now.AddDays(5));
    }

    /// <summary>
    /// A second settlement for the same booking is not stored.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TryAddAsync_ForTheSameBooking_StoresItOnce()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var photographer = Guid.CreateVersion7();
        var booking = Guid.CreateVersion7();

        var first = await repository.TryAddAsync(Pending(photographer, booking, Now), TestContext.Current.CancellationToken);
        var second = await repository.TryAddAsync(Pending(photographer, booking, Now), TestContext.Current.CancellationToken);

        first.ShouldBeTrue();
        second.ShouldBeFalse();
    }

    /// <summary>
    /// Pending refunds are listed per photographer, oldest first, and counted; a completed one leaves the list.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task PendingRefunds_AreListedPerPhotographerOldestFirst()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var mine = Guid.CreateVersion7();
        var older = Pending(mine, Guid.CreateVersion7(), Now);
        var newer = Pending(mine, Guid.CreateVersion7(), Now.AddHours(1));
        var done = Pending(mine, Guid.CreateVersion7(), Now.AddHours(2));
        var foreign = Pending(Guid.CreateVersion7(), Guid.CreateVersion7(), Now);
        foreach (var settlement in new[] { newer, older, done, foreign })
        {
            await repository.TryAddAsync(settlement, TestContext.Current.CancellationToken);
        }

        var stored = (await repository.GetByBookingIdAsync(done.BookingId, TestContext.Current.CancellationToken))!;
        stored.CompleteRefund(PaymentMethod.Cash, null, Now.AddDays(1));
        await repository.UpdateAsync(stored, TestContext.Current.CancellationToken);

        var listed = await repository.ListPendingRefundsAsync(mine, 50, TestContext.Current.CancellationToken);
        var count = await repository.CountPendingRefundsAsync(mine, TestContext.Current.CancellationToken);

        listed.Select(settlement => settlement.Id).ShouldBe([older.Id, newer.Id]);
        count.ShouldBe(2);
    }

    /// <summary>
    /// Writing from a stale copy is rejected.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UpdateAsync_WithAStaleVersion_ThrowsConflict()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var settlement = Pending(Guid.CreateVersion7(), Guid.CreateVersion7(), Now);
        await repository.TryAddAsync(settlement, TestContext.Current.CancellationToken);
        var first = (await repository.GetByBookingIdAsync(settlement.BookingId, TestContext.Current.CancellationToken))!;
        var second = (await repository.GetByBookingIdAsync(settlement.BookingId, TestContext.Current.CancellationToken))!;
        first.CompleteRefund(PaymentMethod.Cash, null, Now);
        await repository.UpdateAsync(first, TestContext.Current.CancellationToken);
        second.Void(Now);

        await Should.ThrowAsync<ConflictException>(() => repository.UpdateAsync(second, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The collection has the unique index per booking.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Indexes_IncludeTheUniqueBookingIndex()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await NewRepositoryAsync();

        var result = await fixture.Database.RunCommandAsync<BsonDocument>(
            new BsonDocument("listIndexes", MongoSettlementRepository.CollectionName),
            cancellationToken: TestContext.Current.CancellationToken);

        var indexes = result["cursor"]["firstBatch"].AsBsonArray.Select(index => index.AsBsonDocument).ToList();
        indexes.Single(index => index["name"] == "ix_settlement_booking")["unique"].AsBoolean.ShouldBeTrue();
        indexes.ShouldContain(index => index["name"] == "ix_settlement_refund");
    }
}
