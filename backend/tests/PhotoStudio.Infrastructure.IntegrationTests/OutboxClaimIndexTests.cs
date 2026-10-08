using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using PhotoStudio.Domain.Bookings.Events;
using PhotoStudio.Infrastructure.Persistence;
using PhotoStudio.Infrastructure.Persistence.Documents;
using PhotoStudio.Infrastructure.Persistence.Outbox;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Guards the performance of the outbox claim: taking the next message must not examine the whole backlog, otherwise
/// draining N messages costs N squared reads and loads the database.
/// </summary>
/// <param name="fixture">Throwaway database shared by the tests of this class.</param>
public sealed class OutboxClaimIndexTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    private const int Backlog = 500;

    /// <summary>
    /// With a large backlog, the query that claims the oldest due message examines a handful of documents, not all of them.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ClaimQuery_WithALargeBacklog_ExaminesOnlyAFewDocuments()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await new MongoIndexInitializer(fixture.Database, NullLogger<MongoIndexInitializer>.Instance).StartAsync(TestContext.Current.CancellationToken);

        var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var messages = Enumerable.Range(0, Backlog)
            .Select(index => new BookingExpired(Guid.CreateVersion7(), now.AddSeconds(index - Backlog)).ToOutboxMessage())
            .ToList();
        var collection = fixture.Database.GetCollection<OutboxMessageDocument>(OutboxProcessor.CollectionName);
        await collection.InsertManyAsync(messages, cancellationToken: TestContext.Current.CancellationToken);

        var explain = await fixture.Database.RunCommandAsync<BsonDocument>(
            new BsonDocument
            {
                {
                    "explain",
                    new BsonDocument
                    {
                        { "find", OutboxProcessor.CollectionName },
                        {
                            "filter",
                            new BsonDocument
                            {
                                { "status", OutboxMessageDocument.PendingStatus },
                                { "nextAttemptAt", new BsonDocument("$lte", now.UtcDateTime) },
                                {
                                    "$or",
                                    new BsonArray
                                    {
                                        new BsonDocument("lockedUntil", BsonNull.Value),
                                        new BsonDocument("lockedUntil", new BsonDocument("$lte", now.UtcDateTime)),
                                    }
                                },
                            }
                        },
                        { "sort", new BsonDocument { { "occurredAt", 1 }, { "_id", 1 } } },
                        { "limit", 1 },
                    }
                },
                { "verbosity", "executionStats" },
            },
            cancellationToken: TestContext.Current.CancellationToken);

        var examined = explain["executionStats"]["totalDocsExamined"].ToInt32();
        examined.ShouldBeLessThan(10);
    }
}
