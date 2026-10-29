using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using PhotoStudio.Domain.Identity;
using PhotoStudio.Infrastructure.Persistence;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Tests of <see cref="MongoEmailVerificationRepository"/> and of the verification fields of <see cref="MongoUserRepository"/>
/// against a real MongoDB replica set: the code is replaced, a guess is counted without losing increments, and the instant of
/// the verification is written once.
/// </summary>
/// <param name="fixture">Throwaway database shared by the tests of this class.</param>
public sealed class MongoEmailVerificationTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A code is stored and read back with all its counters; a user with no code reads as none.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Code_IsStoredAndReadBack()
    {
        await EnsureIndexesAsync();
        var codes = new MongoEmailVerificationRepository(fixture.Database);
        var userId = Guid.CreateVersion7();
        var record = EmailVerification.Issue(userId, "hash-1", null, Now);

        await codes.SaveAsync(record, TestContext.Current.CancellationToken);

        var stored = await codes.GetAsync(userId, TestContext.Current.CancellationToken);
        stored.ShouldBe(record);
        (await codes.GetAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>
    /// A new code replaces the previous one of the same user: there is one record per user.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Save_ReplacesThePreviousCodeOfTheUser()
    {
        await EnsureIndexesAsync();
        var codes = new MongoEmailVerificationRepository(fixture.Database);
        var userId = Guid.CreateVersion7();
        var first = EmailVerification.Issue(userId, "hash-1", null, Now);
        await codes.SaveAsync(first, TestContext.Current.CancellationToken);

        await codes.SaveAsync(EmailVerification.Issue(userId, "hash-2", first, Now.AddMinutes(2)), TestContext.Current.CancellationToken);

        var stored = await codes.GetAsync(userId, TestContext.Current.CancellationToken);
        stored!.CodeHash.ShouldBe("hash-2");
        stored.SendCount.ShouldBe(2);
        (await fixture.Database.GetCollection<BsonDocument>(MongoEmailVerificationRepository.CollectionName)
            .CountDocumentsAsync(new BsonDocument("_id", new BsonBinaryData(userId, GuidRepresentation.Standard)), cancellationToken: TestContext.Current.CancellationToken))
            .ShouldBe(1);
    }

    /// <summary>
    /// Guesses made in parallel are all counted, so they cannot get around the limit.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RegisterAttempt_CountsEveryParallelGuess()
    {
        await EnsureIndexesAsync();
        var codes = new MongoEmailVerificationRepository(fixture.Database);
        var userId = Guid.CreateVersion7();
        await codes.SaveAsync(EmailVerification.Issue(userId, "hash", null, Now), TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => codes.RegisterAttemptAsync(userId, TestContext.Current.CancellationToken)));

        results.Select(result => result!.FailedAttempts).Order().ShouldBe(Enumerable.Range(1, 20));
        (await codes.GetAsync(userId, TestContext.Current.CancellationToken))!.FailedAttempts.ShouldBe(20);
    }

    /// <summary>
    /// Counting a guess for a user with no code finds nothing, and deleting a code removes it.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RegisterAttempt_WithoutACode_ReturnsNull_AndDeleteRemovesTheCode()
    {
        await EnsureIndexesAsync();
        var codes = new MongoEmailVerificationRepository(fixture.Database);
        var userId = Guid.CreateVersion7();
        (await codes.RegisterAttemptAsync(userId, TestContext.Current.CancellationToken)).ShouldBeNull();
        await codes.SaveAsync(EmailVerification.Issue(userId, "hash", null, Now), TestContext.Current.CancellationToken);

        await codes.DeleteAsync(userId, TestContext.Current.CancellationToken);

        (await codes.GetAsync(userId, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>
    /// A user is stored as not verified; marking it stores the instant, and marking it again keeps the first one.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task User_IsMarkedVerifiedOnce_AndKeepsTheFirstInstant()
    {
        await EnsureIndexesAsync();
        var users = new MongoUserRepository(fixture.Database);
        var user = User.Create(Guid.CreateVersion7(), $"u{Guid.NewGuid():N}"[..13], $"{Guid.NewGuid():N}@example.com", "stored-hash", "Ana Pérez", "+50670189220", Now);
        await users.AddAsync(user, TestContext.Current.CancellationToken);
        (await users.GetByIdAsync(user.Id, TestContext.Current.CancellationToken))!.IsEmailVerified.ShouldBeFalse();

        await users.MarkEmailVerifiedAsync(user.Id, Now.AddMinutes(1), TestContext.Current.CancellationToken);
        await users.MarkEmailVerifiedAsync(user.Id, Now.AddMinutes(9), TestContext.Current.CancellationToken);

        var stored = await users.GetByIdAsync(user.Id, TestContext.Current.CancellationToken);
        stored!.IsEmailVerified.ShouldBeTrue();
        stored.EmailVerifiedAt.ShouldBe(Now.AddMinutes(1));
    }

    /// <summary>
    /// The index that removes codes an hour after they expire exists.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Collection_HasTheRetentionIndex()
    {
        await EnsureIndexesAsync();

        var indexes = await (await fixture.Database.GetCollection<BsonDocument>(MongoEmailVerificationRepository.CollectionName).Indexes
            .ListAsync(TestContext.Current.CancellationToken)).ToListAsync(TestContext.Current.CancellationToken);

        var retention = indexes.Single(index => index["name"].AsString == "ix_email_verification_retention");
        retention["expireAfterSeconds"].ToInt32().ShouldBe(3600);
    }

    /// <summary>
    /// Skips the test when MongoDB is not reachable and creates the indexes of the throwaway database.
    /// </summary>
    /// <returns>A task that completes when the indexes exist.</returns>
    private async Task EnsureIndexesAsync()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await new MongoIndexInitializer(fixture.Database, NullLogger<MongoIndexInitializer>.Instance).StartAsync(TestContext.Current.CancellationToken);
    }
}
