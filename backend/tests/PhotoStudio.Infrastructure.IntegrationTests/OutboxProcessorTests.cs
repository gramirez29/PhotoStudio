using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Bookings.Events;
using PhotoStudio.Infrastructure.Persistence.Documents;
using PhotoStudio.Infrastructure.Persistence.Outbox;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Tests of <see cref="OutboxProcessor"/> against a real MongoDB replica set: delivery, retries with backoff, leases and
/// messages that cannot be read.
/// </summary>
/// <param name="fixture">Throwaway database shared by the tests of this class.</param>
public sealed class OutboxProcessorTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A pending message reaches the registered handler once and is marked as processed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ProcessBatchAsync_DeliversTheEventAndMarksTheMessageProcessed()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await ResetAsync();
        var probe = new BookingCreatedProbe();
        var clock = new MutableTimeProvider(Start);
        var created = NewEvent();
        var message = await InsertMessageAsync(created);

        var claimed = await NewProcessor(probe, clock).ProcessBatchAsync(10, TestContext.Current.CancellationToken);

        claimed.ShouldBe(1);
        probe.Received.Count.ShouldBe(1);
        probe.Received[0].ShouldBe(created);
        var stored = await GetMessageAsync(message.Id);
        stored.Status.ShouldBe(OutboxMessageDocument.ProcessedStatus);
        stored.ProcessedAt.ShouldNotBeNull();
        stored.LockedUntil.ShouldBeNull();
    }

    /// <summary>
    /// A message processed once is never delivered again.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ProcessBatchAsync_DoesNotDeliverAProcessedMessageAgain()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await ResetAsync();
        var probe = new BookingCreatedProbe();
        var processor = NewProcessor(probe, new MutableTimeProvider(Start));
        await InsertMessageAsync(NewEvent());

        await processor.ProcessBatchAsync(10, TestContext.Current.CancellationToken);
        var second = await processor.ProcessBatchAsync(10, TestContext.Current.CancellationToken);

        second.ShouldBe(0);
        probe.Received.Count.ShouldBe(1);
    }

    /// <summary>
    /// An event nobody listens to is still marked as processed, so the queue does not grow.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ProcessBatchAsync_WithoutHandlers_MarksTheMessageProcessed()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await ResetAsync();
        var message = await InsertMessageAsync(new BookingExpired(Guid.CreateVersion7(), Start));

        await NewProcessor(null, new MutableTimeProvider(Start)).ProcessBatchAsync(10, TestContext.Current.CancellationToken);

        (await GetMessageAsync(message.Id)).Status.ShouldBe(OutboxMessageDocument.ProcessedStatus);
    }

    /// <summary>
    /// When the handler fails the message stays pending and is retried only after the backoff delay.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ProcessBatchAsync_WhenTheHandlerFails_RetriesAfterTheBackoff()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await ResetAsync();
        var probe = new BookingCreatedProbe { FailuresRemaining = 1 };
        var clock = new MutableTimeProvider(Start);
        var processor = NewProcessor(probe, clock);
        var message = await InsertMessageAsync(NewEvent());

        await processor.ProcessBatchAsync(10, TestContext.Current.CancellationToken);

        var afterFailure = await GetMessageAsync(message.Id);
        afterFailure.Status.ShouldBe(OutboxMessageDocument.PendingStatus);
        afterFailure.Attempts.ShouldBe(1);
        afterFailure.LastError.ShouldBe("probe failure");
        afterFailure.LockedUntil.ShouldBeNull();

        // Still inside the backoff window: nothing is claimed.
        clock.Advance(TimeSpan.FromSeconds(1));
        (await processor.ProcessBatchAsync(10, TestContext.Current.CancellationToken)).ShouldBe(0);

        // Past the first delay (5 s): the retry succeeds.
        clock.Advance(TimeSpan.FromSeconds(5));
        (await processor.ProcessBatchAsync(10, TestContext.Current.CancellationToken)).ShouldBe(1);

        var afterRetry = await GetMessageAsync(message.Id);
        afterRetry.Status.ShouldBe(OutboxMessageDocument.ProcessedStatus);
        afterRetry.Attempts.ShouldBe(2);
        probe.Received.Count.ShouldBe(1);
    }

    /// <summary>
    /// A message that keeps failing is parked as failed once it runs out of attempts, and is not claimed again.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ProcessBatchAsync_AfterTheMaximumAttempts_ParksTheMessage()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await ResetAsync();
        var probe = new BookingCreatedProbe { FailuresRemaining = int.MaxValue };
        var clock = new MutableTimeProvider(Start);
        var processor = NewProcessor(probe, clock);
        var message = await InsertMessageAsync(NewEvent());

        for (var attempt = 0; attempt < OutboxProcessor.MaxAttempts; attempt++)
        {
            clock.Advance(TimeSpan.FromHours(1));
            await processor.ProcessBatchAsync(10, TestContext.Current.CancellationToken);
        }

        var stored = await GetMessageAsync(message.Id);
        stored.Status.ShouldBe(OutboxMessageDocument.FailedStatus);
        stored.Attempts.ShouldBe(OutboxProcessor.MaxAttempts);
        clock.Advance(TimeSpan.FromHours(1));
        (await processor.ProcessBatchAsync(10, TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    /// <summary>
    /// A message that cannot be deserialized is parked immediately instead of being retried.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ProcessBatchAsync_WithUnknownEventType_ParksTheMessageAtOnce()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await ResetAsync();
        var message = NewEvent().ToOutboxMessage();
        message.Type = "NoSuchEvent";
        await Messages.InsertOneAsync(message, cancellationToken: TestContext.Current.CancellationToken);

        await NewProcessor(new BookingCreatedProbe(), new MutableTimeProvider(Start)).ProcessBatchAsync(10, TestContext.Current.CancellationToken);

        var stored = await GetMessageAsync(message.Id);
        stored.Status.ShouldBe(OutboxMessageDocument.FailedStatus);
        stored.Attempts.ShouldBe(1);
    }

    /// <summary>
    /// A message held by another worker is left alone until its lease ends, then it is taken over.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ProcessBatchAsync_RespectsTheLeaseOfAnotherWorker()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await ResetAsync();
        var probe = new BookingCreatedProbe();
        var clock = new MutableTimeProvider(Start);
        var processor = NewProcessor(probe, clock);
        var message = NewEvent().ToOutboxMessage();
        message.LockedUntil = (Start + OutboxProcessor.LeaseDuration).UtcDateTime;
        await Messages.InsertOneAsync(message, cancellationToken: TestContext.Current.CancellationToken);

        (await processor.ProcessBatchAsync(10, TestContext.Current.CancellationToken)).ShouldBe(0);

        clock.Advance(OutboxProcessor.LeaseDuration + TimeSpan.FromSeconds(1));
        (await processor.ProcessBatchAsync(10, TestContext.Current.CancellationToken)).ShouldBe(1);
        probe.Received.Count.ShouldBe(1);
    }

    /// <summary>
    /// Messages are delivered in the order the events occurred.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ProcessBatchAsync_DeliversOldestEventFirst()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await ResetAsync();
        var probe = new BookingCreatedProbe();
        var older = NewEvent(Start.AddMinutes(-10));
        var newer = NewEvent(Start.AddMinutes(-5));
        await InsertMessageAsync(newer);
        await InsertMessageAsync(older);

        await NewProcessor(probe, new MutableTimeProvider(Start)).ProcessBatchAsync(10, TestContext.Current.CancellationToken);

        probe.Received.Select(received => received.BookingId).ShouldBe([older.BookingId, newer.BookingId]);
    }

    private IMongoCollection<OutboxMessageDocument> Messages =>
        fixture.Database.GetCollection<OutboxMessageDocument>(OutboxProcessor.CollectionName);

    /// <summary>
    /// Builds a booking-created event for a new booking.
    /// </summary>
    /// <param name="occurredAt">Instant of the event; defaults to the test start.</param>
    /// <returns>The event.</returns>
    private static BookingCreated NewEvent(DateTimeOffset? occurredAt = null) => new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        Start.AddDays(10),
        Start.AddDays(2),
        occurredAt ?? Start);

    /// <summary>
    /// Removes the messages left by previous tests of this class.
    /// </summary>
    /// <returns>A task that completes when the collection is empty.</returns>
    private Task ResetAsync() => Messages.DeleteManyAsync(FilterDefinition<OutboxMessageDocument>.Empty, TestContext.Current.CancellationToken);

    /// <summary>
    /// Stores a pending message for the event.
    /// </summary>
    /// <param name="domainEvent">Event to store.</param>
    /// <returns>The stored message.</returns>
    private async Task<OutboxMessageDocument> InsertMessageAsync(PhotoStudio.Domain.Common.IDomainEvent domainEvent)
    {
        var message = domainEvent.ToOutboxMessage();
        await Messages.InsertOneAsync(message, cancellationToken: TestContext.Current.CancellationToken);
        return message;
    }

    /// <summary>
    /// Reads a message back from the database.
    /// </summary>
    /// <param name="id">Message identifier.</param>
    /// <returns>The stored message.</returns>
    private async Task<OutboxMessageDocument> GetMessageAsync(Guid id) =>
        await Messages.Find(message => message.Id == id).SingleAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// Creates a processor whose handlers are the given probe (when any).
    /// </summary>
    /// <param name="probe">Handler registered for <see cref="BookingCreated"/>, or <see langword="null"/> for none.</param>
    /// <param name="clock">Clock of the processor.</param>
    /// <returns>The processor.</returns>
    private OutboxProcessor NewProcessor(BookingCreatedProbe? probe, TimeProvider clock)
    {
        var services = new ServiceCollection();
        if (probe is not null)
        {
            services.AddSingleton<IDomainEventHandler<BookingCreated>>(probe);
        }

        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new OutboxProcessor(fixture.Database, scopeFactory, clock, NullLogger<OutboxProcessor>.Instance);
    }

    /// <summary>
    /// Handler that records the events it receives and can be told to fail a number of times first.
    /// </summary>
    private sealed class BookingCreatedProbe : IDomainEventHandler<BookingCreated>
    {
        /// <summary>
        /// Gets the events handled successfully.
        /// </summary>
        public List<BookingCreated> Received { get; } = [];

        /// <summary>
        /// Gets or sets how many calls still fail before the handler starts succeeding.
        /// </summary>
        public int FailuresRemaining { get; set; }

        /// <summary>
        /// Records the event, or throws while failures remain.
        /// </summary>
        /// <param name="domainEvent">Delivered event.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A completed task.</returns>
        /// <exception cref="InvalidOperationException">While <see cref="FailuresRemaining"/> is positive.</exception>
        public Task HandleAsync(BookingCreated domainEvent, CancellationToken cancellationToken)
        {
            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;
                throw new InvalidOperationException("probe failure");
            }

            Received.Add(domainEvent);
            return Task.CompletedTask;
        }
    }
}
