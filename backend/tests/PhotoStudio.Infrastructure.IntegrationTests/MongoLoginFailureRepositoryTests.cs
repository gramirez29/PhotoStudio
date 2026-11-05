using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using PhotoStudio.Infrastructure.Persistence;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Tests of <see cref="MongoLoginFailureRepository"/> against a real MongoDB replica set, focused on what must hold under
/// concurrency and across accounts and addresses: no guess is lost, and what is counted against one address or account never
/// touches another.
/// </summary>
/// <param name="fixture">Throwaway database shared by the tests of this class.</param>
public sealed class MongoLoginFailureRepositoryTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Parallel failures from the same address are all counted. With load-modify-save most would be lost and a guesser would never
    /// reach the limit.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RegisterFailureAsync_CountsEveryParallelAttempt()
    {
        var userId = Guid.CreateVersion7();

        var counts = await Task.WhenAll(
            Enumerable.Range(0, 25).Select(_ => NewRepository().RegisterFailureAsync(userId, "address-a", Now, TestContext.Current.CancellationToken)));

        counts.Order().ShouldBe(Enumerable.Range(1, 25));
        (await NewRepository().GetAsync(userId, "address-a", TestContext.Current.CancellationToken))!.Attempts.ShouldBe(25);
    }

    /// <summary>
    /// Blocking stores the end of the block and restarts the counter; the address is refused until then and not after.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task BlockAsync_RefusesTheAddressUntilTheEndAndRestartsTheCounter()
    {
        var repository = NewRepository();
        var userId = Guid.CreateVersion7();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await repository.RegisterFailureAsync(userId, "address-a", Now, TestContext.Current.CancellationToken);
        }

        await repository.BlockAsync(userId, "address-a", Now.AddMinutes(15), TestContext.Current.CancellationToken);
        var blocked = await repository.GetAsync(userId, "address-a", TestContext.Current.CancellationToken);

        blocked!.Attempts.ShouldBe(0);
        blocked.BlockedUntil.ShouldBe(Now.AddMinutes(15));
        blocked.IsBlockedAt(Now).ShouldBeTrue();
        blocked.IsBlockedAt(Now.AddMinutes(15)).ShouldBeFalse();
        (await repository.RegisterFailureAsync(userId, "address-a", Now.AddMinutes(16), TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    /// <summary>
    /// Blocking an address that has no record yet creates it, so the block does not depend on the order of the calls.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task BlockAsync_WithoutARecord_CreatesIt()
    {
        var userId = Guid.CreateVersion7();

        await NewRepository().BlockAsync(userId, "address-a", Now.AddMinutes(15), TestContext.Current.CancellationToken);

        (await NewRepository().GetAsync(userId, "address-a", TestContext.Current.CancellationToken))!.IsBlockedAt(Now).ShouldBeTrue();
    }

    /// <summary>
    /// What is counted against one address of an account, or against one account, never touches another address or account.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Failures_AreKeptApartByAddressAndByAccount()
    {
        var repository = NewRepository();
        var user = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        await repository.RegisterFailureAsync(user, "address-a", Now, TestContext.Current.CancellationToken);
        await repository.RegisterFailureAsync(user, "address-a", Now, TestContext.Current.CancellationToken);
        await repository.BlockAsync(user, "address-b", Now.AddMinutes(15), TestContext.Current.CancellationToken);

        (await repository.GetAsync(user, "address-a", TestContext.Current.CancellationToken))!.Attempts.ShouldBe(2);
        (await repository.GetAsync(user, "address-a", TestContext.Current.CancellationToken))!.IsBlockedAt(Now).ShouldBeFalse();
        (await repository.GetAsync(user, "address-b", TestContext.Current.CancellationToken))!.IsBlockedAt(Now).ShouldBeTrue();
        (await repository.GetAsync(other, "address-a", TestContext.Current.CancellationToken)).ShouldBeNull();
        (await repository.GetAsync(user, "address-c", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>
    /// Deleting forgets one address; deleting for the account forgets all of its addresses and no other account's.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Delete_ForgetsOneAddress_AndDeleteAllForgetsTheWholeAccountOnly()
    {
        var repository = NewRepository();
        var user = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        await repository.RegisterFailureAsync(user, "address-a", Now, TestContext.Current.CancellationToken);
        await repository.RegisterFailureAsync(user, "address-b", Now, TestContext.Current.CancellationToken);
        await repository.RegisterFailureAsync(other, "address-a", Now, TestContext.Current.CancellationToken);

        await repository.DeleteAsync(user, "address-a", TestContext.Current.CancellationToken);

        (await repository.GetAsync(user, "address-a", TestContext.Current.CancellationToken)).ShouldBeNull();
        (await repository.GetAsync(user, "address-b", TestContext.Current.CancellationToken)).ShouldNotBeNull();

        await repository.DeleteAllForUserAsync(user, TestContext.Current.CancellationToken);

        (await repository.GetAsync(user, "address-b", TestContext.Current.CancellationToken)).ShouldBeNull();
        (await repository.GetAsync(other, "address-a", TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    /// <summary>
    /// The collection forgets old records by itself (retention index) and can find the records of an account.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Indexes_AreCreated_WithRetentionThatExpiresAtTheStoredInstant()
    {
        await new MongoIndexInitializer(fixture.Database, NullLogger<MongoIndexInitializer>.Instance).StartAsync(TestContext.Current.CancellationToken);

        var listing = await fixture.Database.RunCommandAsync<BsonDocument>(
            new BsonDocument("listIndexes", MongoLoginFailureRepository.CollectionName),
            cancellationToken: TestContext.Current.CancellationToken);
        var indexes = listing["cursor"]["firstBatch"].AsBsonArray.Select(index => index.AsBsonDocument).ToList();

        indexes.Single(index => index["name"].AsString == "ix_login_failure_retention")["expireAfterSeconds"].ToInt32().ShouldBe(0);
        indexes.ShouldContain(index => index["name"].AsString == "ix_login_failure_user");
    }

    /// <summary>
    /// Builds a repository over the test database.
    /// </summary>
    /// <returns>The repository.</returns>
    private MongoLoginFailureRepository NewRepository() => new(fixture.Database);
}
