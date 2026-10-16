using Microsoft.Extensions.Logging.Abstractions;
using PhotoStudio.Domain.Portal;
using PhotoStudio.Infrastructure.Persistence;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Tests of <see cref="MongoBookingLinkRepository"/> and <see cref="MongoPortalOtpRepository"/> against a real MongoDB
/// replica set, focused on what must hold under concurrency: a booking never has two working links, and parallel guesses of a
/// verification code each get their own number.
/// </summary>
/// <param name="fixture">Throwaway database shared by the tests of this class.</param>
public sealed class MongoPortalRepositoriesTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A link is found by the hash of its token with all its data.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Link_IsStoredAndFoundByTheHashOfItsToken()
    {
        await EnsureIndexesAsync();
        var links = new MongoBookingLinkRepository(fixture.Database);
        var link = NewLink(Guid.CreateVersion7());

        await links.ReplaceActiveAsync(link, Now, TestContext.Current.CancellationToken);

        var found = await links.GetByTokenHashAsync(link.TokenHash, TestContext.Current.CancellationToken);
        found.ShouldNotBeNull();
        found.BookingId.ShouldBe(link.BookingId);
        found.PhotographerId.ShouldBe(link.PhotographerId);
        found.ExpiresAt.ShouldBe(link.ExpiresAt);
        found.RevokedAt.ShouldBeNull();
        (await links.GetByTokenHashAsync("unknown", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>
    /// Creating a new link for a booking revokes the previous one, so only the latest works.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ReplaceActive_RevokesThePreviousLinkOfTheBooking()
    {
        await EnsureIndexesAsync();
        var links = new MongoBookingLinkRepository(fixture.Database);
        var bookingId = Guid.CreateVersion7();
        var first = NewLink(bookingId);
        var second = NewLink(bookingId);
        var otherBooking = NewLink(Guid.CreateVersion7());
        await links.ReplaceActiveAsync(first, Now, TestContext.Current.CancellationToken);
        await links.ReplaceActiveAsync(otherBooking, Now, TestContext.Current.CancellationToken);

        await links.ReplaceActiveAsync(second, Now.AddMinutes(5), TestContext.Current.CancellationToken);

        var oldLink = await links.GetByTokenHashAsync(first.TokenHash, TestContext.Current.CancellationToken);
        oldLink.ShouldNotBeNull();
        oldLink.RevokedAt.ShouldBe(Now.AddMinutes(5));
        oldLink.IsActiveAt(Now.AddMinutes(6)).ShouldBeFalse();
        (await links.GetByTokenHashAsync(second.TokenHash, TestContext.Current.CancellationToken))!.IsActiveAt(Now.AddMinutes(6)).ShouldBeTrue();
        (await links.GetByTokenHashAsync(otherBooking.TokenHash, TestContext.Current.CancellationToken))!.IsActiveAt(Now.AddMinutes(6)).ShouldBeTrue();
    }

    /// <summary>
    /// A code record is stored, read back and replaced by the next one of the same booking.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Code_IsStoredReadAndReplaced()
    {
        await EnsureIndexesAsync();
        var codes = new MongoPortalOtpRepository(fixture.Database);
        var bookingId = Guid.CreateVersion7();
        var first = PortalOtp.Issue(bookingId, "hash-1", null, Now);

        await codes.SaveAsync(first, TestContext.Current.CancellationToken);
        var stored = await codes.GetAsync(bookingId, TestContext.Current.CancellationToken);
        await codes.SaveAsync(PortalOtp.Issue(bookingId, "hash-2", first, Now.AddMinutes(2)), TestContext.Current.CancellationToken);
        var replaced = await codes.GetAsync(bookingId, TestContext.Current.CancellationToken);

        stored.ShouldBe(first);
        replaced.ShouldNotBeNull();
        replaced.CodeHash.ShouldBe("hash-2");
        replaced.SendCount.ShouldBe(2);
        replaced.WindowStartedAt.ShouldBe(Now);
    }

    /// <summary>
    /// Guesses sent in parallel are counted one by one: none is lost and none shares a number, so the limit cannot be
    /// slipped past by speed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RegisterAttempt_CountsParallelGuessesWithoutLosingAny()
    {
        await EnsureIndexesAsync();
        var codes = new MongoPortalOtpRepository(fixture.Database);
        var bookingId = Guid.CreateVersion7();
        await codes.SaveAsync(PortalOtp.Issue(bookingId, "hash", null, Now), TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => codes.RegisterAttemptAsync(bookingId, TestContext.Current.CancellationToken)));

        results.Select(otp => otp!.FailedAttempts).OrderBy(count => count).ShouldBe(Enumerable.Range(1, 40));
        (await codes.GetAsync(bookingId, TestContext.Current.CancellationToken))!.FailedAttempts.ShouldBe(40);
    }

    /// <summary>
    /// With no code requested there is nothing to count, and a deleted code is gone.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RegisterAttempt_WithoutACode_ReturnsNull_AndDeleteRemovesIt()
    {
        await EnsureIndexesAsync();
        var codes = new MongoPortalOtpRepository(fixture.Database);
        var bookingId = Guid.CreateVersion7();

        (await codes.RegisterAttemptAsync(bookingId, TestContext.Current.CancellationToken)).ShouldBeNull();

        await codes.SaveAsync(PortalOtp.Issue(bookingId, "hash", null, Now), TestContext.Current.CancellationToken);
        await codes.DeleteAsync(bookingId, TestContext.Current.CancellationToken);

        (await codes.GetAsync(bookingId, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await codes.RegisterAttemptAsync(bookingId, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>
    /// Creates the indexes of the throwaway database, or skips the test when MongoDB is not reachable.
    /// </summary>
    /// <returns>A task that completes when the indexes exist.</returns>
    private async Task EnsureIndexesAsync()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await new MongoIndexInitializer(fixture.Database, NullLogger<MongoIndexInitializer>.Instance).StartAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Builds an active link for a booking with a unique token hash.
    /// </summary>
    /// <param name="bookingId">Booking the link opens.</param>
    /// <returns>The link.</returns>
    private static BookingLink NewLink(Guid bookingId) =>
        BookingLink.Issue(bookingId, Guid.CreateVersion7(), Guid.NewGuid().ToString("N"), Now, Now.AddDays(30));
}
