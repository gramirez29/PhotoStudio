using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using PhotoStudio.Domain.Identity;
using PhotoStudio.Infrastructure.Persistence;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Tests of the repositories of the web area against a real MongoDB: sessions, remembered browsers and challenges, and the
/// guarantees that matter when requests arrive at the same time.
/// </summary>
/// <param name="fixture">Throwaway database shared by the tests of this class.</param>
public sealed class MongoStudioRepositoriesTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A session is found by the hash of its secret, shows when it was used, and goes away when it is closed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Session_IsStoredFoundTouchedAndClosed()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await EnsureIndexesAsync();
        var repository = new MongoStudioSessionRepository(fixture.Database);
        var session = StudioSession.Start(Guid.CreateVersion7(), $"hash-{Guid.NewGuid():N}", Now);

        await repository.AddAsync(session, TestContext.Current.CancellationToken);
        var found = await repository.GetByTokenHashAsync(session.TokenHash, TestContext.Current.CancellationToken);
        await repository.TouchAsync(session.Id, Now.AddMinutes(5), TestContext.Current.CancellationToken);
        var touched = await repository.GetByTokenHashAsync(session.TokenHash, TestContext.Current.CancellationToken);
        await repository.DeleteAsync(session.Id, TestContext.Current.CancellationToken);

        found.ShouldBe(session);
        touched!.LastSeenAt.ShouldBe(Now.AddMinutes(5));
        touched.ExpiresAt.ShouldBe(session.ExpiresAt);
        (await repository.GetByTokenHashAsync(session.TokenHash, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await repository.GetByTokenHashAsync("never-stored", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>
    /// Closing every session of a user leaves the sessions of other users.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Session_DeleteAllForUser_LeavesOtherUsersAlone()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await EnsureIndexesAsync();
        var repository = new MongoStudioSessionRepository(fixture.Database);
        var user = Guid.CreateVersion7();
        var mine = new[] { StudioSession.Start(user, $"a-{Guid.NewGuid():N}", Now), StudioSession.Start(user, $"b-{Guid.NewGuid():N}", Now) };
        var other = StudioSession.Start(Guid.CreateVersion7(), $"c-{Guid.NewGuid():N}", Now);
        foreach (var session in mine.Append(other))
        {
            await repository.AddAsync(session, TestContext.Current.CancellationToken);
        }

        await repository.DeleteAllForUserAsync(user, TestContext.Current.CancellationToken);

        foreach (var session in mine)
        {
            (await repository.GetByTokenHashAsync(session.TokenHash, TestContext.Current.CancellationToken)).ShouldBeNull();
        }

        (await repository.GetByTokenHashAsync(other.TokenHash, TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    /// <summary>
    /// Two sessions cannot share a secret: the hash is unique.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Session_TwoWithTheSameHash_AreRefused()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await EnsureIndexesAsync();
        var repository = new MongoStudioSessionRepository(fixture.Database);
        var hash = $"same-{Guid.NewGuid():N}";
        await repository.AddAsync(StudioSession.Start(Guid.CreateVersion7(), hash, Now), TestContext.Current.CancellationToken);

        await Should.ThrowAsync<MongoWriteException>(() => repository.AddAsync(StudioSession.Start(Guid.CreateVersion7(), hash, Now), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Ten requests that arrive with the same cookie of a remembered browser cannot all rotate it: exactly one does, and the old
    /// secret stops working.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Device_RotatedInParallel_RotatesExactlyOnce()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await EnsureIndexesAsync();
        var repository = new MongoStudioDeviceRepository(fixture.Database);
        var oldHash = $"old-{Guid.NewGuid():N}";
        var device = StudioTrustedDevice.Remember(Guid.CreateVersion7(), oldHash, Now);
        await repository.AddAsync(device, TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(index =>
            repository.RotateAsync(device.Id, oldHash, $"new-{index}-{Guid.NewGuid():N}", Now.AddDays(1), Now.AddDays(31), TestContext.Current.CancellationToken)));

        results.Count(rotated => rotated).ShouldBe(1);
        (await repository.GetByTokenHashAsync(oldHash, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>
    /// Rotating moves the expiry forward and keeps the user; forgetting removes one device, and forgetting a user removes all theirs.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Device_RotationPushesTheExpiryForward_AndDevicesCanBeForgotten()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await EnsureIndexesAsync();
        var repository = new MongoStudioDeviceRepository(fixture.Database);
        var user = Guid.CreateVersion7();
        var first = StudioTrustedDevice.Remember(user, $"one-{Guid.NewGuid():N}", Now);
        var second = StudioTrustedDevice.Remember(user, $"two-{Guid.NewGuid():N}", Now);
        await repository.AddAsync(first, TestContext.Current.CancellationToken);
        await repository.AddAsync(second, TestContext.Current.CancellationToken);

        var newHash = $"rotated-{Guid.NewGuid():N}";
        (await repository.RotateAsync(first.Id, first.TokenHash, newHash, Now.AddDays(10), Now.AddDays(40), TestContext.Current.CancellationToken)).ShouldBeTrue();
        var rotated = await repository.GetByTokenHashAsync(newHash, TestContext.Current.CancellationToken);
        await repository.DeleteAsync(first.Id, TestContext.Current.CancellationToken);

        rotated!.UserId.ShouldBe(user);
        rotated.ExpiresAt.ShouldBe(Now.AddDays(40));
        rotated.LastUsedAt.ShouldBe(Now.AddDays(10));
        (await repository.GetByTokenHashAsync(newHash, TestContext.Current.CancellationToken)).ShouldBeNull();
        await repository.DeleteAllForUserAsync(user, TestContext.Current.CancellationToken);
        (await repository.GetByTokenHashAsync(second.TokenHash, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>
    /// A user has one challenge: saving another replaces it, and the old secret stops finding anything.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Challenge_SavingReplacesTheOneOfTheUser()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await EnsureIndexesAsync();
        var repository = new MongoStudioChallengeRepository(fixture.Database);
        var user = Guid.CreateVersion7();
        var first = StudioLoginChallenge.Issue(user, $"first-{Guid.NewGuid():N}", "code-one", null, Now);
        var second = StudioLoginChallenge.Issue(user, $"second-{Guid.NewGuid():N}", "code-two", first, Now.AddMinutes(2));

        await repository.SaveAsync(first, TestContext.Current.CancellationToken);
        await repository.SaveAsync(second, TestContext.Current.CancellationToken);

        (await repository.GetByTokenHashAsync(first.TokenHash, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await repository.GetByTokenHashAsync(second.TokenHash, TestContext.Current.CancellationToken)).ShouldBe(second);
        (await repository.GetByUserIdAsync(user, TestContext.Current.CancellationToken)).ShouldBe(second);
        second.SendCount.ShouldBe(2);
    }

    /// <summary>
    /// Twenty guesses made at the same time are all counted: the increment is atomic, so parallel guesses cannot get past the limit.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Challenge_GuessesInParallel_AreAllCounted()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await EnsureIndexesAsync();
        var repository = new MongoStudioChallengeRepository(fixture.Database);
        var challenge = StudioLoginChallenge.Issue(Guid.CreateVersion7(), $"guess-{Guid.NewGuid():N}", "code", null, Now);
        await repository.SaveAsync(challenge, TestContext.Current.CancellationToken);

        var counts = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
            (await repository.RegisterAttemptAsync(challenge.TokenHash, TestContext.Current.CancellationToken))!.FailedAttempts));

        counts.OrderBy(count => count).ShouldBe(Enumerable.Range(1, 20));
        (await repository.RegisterAttemptAsync("unknown-secret", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>
    /// Ten requests that try to spend the same challenge at the same time: exactly one succeeds.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Challenge_DeletedInParallel_IsSpentExactlyOnce()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await EnsureIndexesAsync();
        var repository = new MongoStudioChallengeRepository(fixture.Database);
        var user = Guid.CreateVersion7();
        await repository.SaveAsync(StudioLoginChallenge.Issue(user, $"spend-{Guid.NewGuid():N}", "code", null, Now), TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => repository.DeleteAsync(user, TestContext.Current.CancellationToken)));

        results.Count(deleted => deleted).ShouldBe(1);
        (await repository.GetByUserIdAsync(user, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>
    /// The indexes exist after startup: unique hashes, a lookup by user, and the ones that remove sessions, devices and challenges
    /// by themselves when they end.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Startup_CreatesTheIndexesOfTheWebArea()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await EnsureIndexesAsync();

        var sessions = await IndexesOfAsync(MongoStudioSessionRepository.CollectionName);
        var devices = await IndexesOfAsync(MongoStudioDeviceRepository.CollectionName);
        var challenges = await IndexesOfAsync(MongoStudioChallengeRepository.CollectionName);

        sessions.ShouldContain(index => index["name"] == "ix_studio_session_hash" && index.GetValue("unique", false).AsBoolean);
        sessions.ShouldContain(index => index["name"] == "ix_studio_session_user");
        sessions.ShouldContain(index => index["name"] == "ix_studio_session_retention" && index.Contains("expireAfterSeconds"));
        devices.ShouldContain(index => index["name"] == "ix_studio_device_hash" && index.GetValue("unique", false).AsBoolean);
        devices.ShouldContain(index => index["name"] == "ix_studio_device_user");
        devices.ShouldContain(index => index["name"] == "ix_studio_device_retention" && index.Contains("expireAfterSeconds"));
        challenges.ShouldContain(index => index["name"] == "ix_studio_challenge_hash" && index.GetValue("unique", false).AsBoolean);
        challenges.ShouldContain(index => index["name"] == "ix_studio_challenge_retention" && index.Contains("expireAfterSeconds"));
    }

    /// <summary>
    /// Creates the indexes the way the API does at startup.
    /// </summary>
    /// <returns>A task that completes when they exist.</returns>
    private Task EnsureIndexesAsync() =>
        new MongoIndexInitializer(fixture.Database, NullLogger<MongoIndexInitializer>.Instance).StartAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// Lists the indexes of a collection.
    /// </summary>
    /// <param name="collection">Collection name.</param>
    /// <returns>The index definitions.</returns>
    private async Task<List<BsonDocument>> IndexesOfAsync(string collection) =>
        await (await fixture.Database.GetCollection<BsonDocument>(collection).Indexes.ListAsync(TestContext.Current.CancellationToken)).ToListAsync(TestContext.Current.CancellationToken);
}
