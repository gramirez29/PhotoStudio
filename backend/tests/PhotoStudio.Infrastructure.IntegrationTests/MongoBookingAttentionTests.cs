using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using PhotoStudio.Infrastructure.Persistence;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Tests of <see cref="MongoBookingRepository.CountWithPendingProofAsync"/> and <see cref="MongoBookingRepository.CountExpiringTentativeAsync"/>
/// against a real MongoDB replica set: what is waiting for the photographer, and only theirs.
/// </summary>
/// <param name="fixture">Throwaway database shared by the tests of this class.</param>
public sealed class MongoBookingAttentionTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    private const string SkipReason = "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.";

    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Session = new(2026, 12, 20, 18, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A booking counts once, however many proofs it has, only while a proof waits for review, and never for another photographer.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task CountWithPendingProofAsync_CountsTheBookingsWithAProofToReview()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var mine = Guid.CreateVersion7();
        var theirs = Guid.CreateVersion7();
        var repository = new MongoBookingRepository(fixture.Database);
        await AddAsync(mine, Session, proofs: 2);
        await AddAsync(mine, Session.AddDays(1), proofs: 1);
        await AddAsync(mine, Session.AddDays(2), proofs: 0);
        await AddAsync(theirs, Session, proofs: 1);

        var count = await repository.CountWithPendingProofAsync(mine, TestContext.Current.CancellationToken);

        count.ShouldBe(2);
    }

    /// <summary>
    /// Only the tentative bookings whose hold ends by the instant count, and a proof waiting for review means it cannot expire.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task CountExpiringTentativeAsync_CountsTheHoldsThatEndSoon_AndLeavesOutTheOnesWithAProof()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var mine = Guid.CreateVersion7();
        var repository = new MongoBookingRepository(fixture.Database);
        // The hold ends when the session starts if that is sooner than 48 hours, so sessions today are the ones that expire soon.
        await AddAsync(mine, Now.AddHours(5), proofs: 0);
        await AddAsync(mine, Now.AddHours(8), proofs: 0);
        await AddAsync(mine, Now.AddHours(10), proofs: 1);
        await AddAsync(mine, Session, proofs: 0);
        await AddAsync(Guid.CreateVersion7(), Now.AddHours(5), proofs: 0);

        var soon = await repository.CountExpiringTentativeAsync(mine, Now.AddHours(24), TestContext.Current.CancellationToken);
        var none = await repository.CountExpiringTentativeAsync(mine, Now.AddHours(1), TestContext.Current.CancellationToken);

        soon.ShouldBe(2);
        none.ShouldBe(0);
    }

    /// <summary>
    /// Stores a tentative booking with the given number of proofs of payment waiting for review.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="start">Session start; the session lasts an hour.</param>
    /// <param name="proofs">How many proofs to submit.</param>
    /// <returns>A task that completes when the booking is stored.</returns>
    private async Task AddAsync(Guid photographerId, DateTimeOffset start, int proofs)
    {
        var booking = Booking.Create(
            Guid.CreateVersion7(),
            photographerId,
            ClientContact.Create("María Pérez", "+506 8888-8888"),
            "Retrato familiar",
            Money.Create(100_000m, "CRC"),
            TimeSlot.Create(start, start.AddHours(1)),
            BookingPolicy.Default,
            Now);
        for (var index = 0; index < proofs; index++)
        {
            booking.SubmitPaymentProof(Guid.CreateVersion7(), Money.Create(10_000m, "CRC"), PaymentMethod.SinpeMovil, $"proof-{index}", Now);
        }

        await new MongoBookingRepository(fixture.Database).AddAsync(booking, TestContext.Current.CancellationToken);
    }
}
