using MongoDB.Driver;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using PhotoStudio.Infrastructure.Persistence;
using PhotoStudio.Infrastructure.Persistence.Documents;
using PhotoStudio.Infrastructure.Persistence.Outbox;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Tests that <see cref="MongoBookingRepository"/> stores the domain events in the outbox together with the change that
/// raised them, and of the query that feeds the expiration job. Run against a real MongoDB replica set.
/// </summary>
/// <param name="fixture">Throwaway database shared by the tests of this class.</param>
public sealed class MongoBookingRepositoryOutboxTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SessionStart = new(2026, 10, 20, 15, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Adding a booking stores its creation event in the outbox and clears the pending events of the aggregate.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AddAsync_StoresTheCreationEventInTheOutbox()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var booking = NewBooking(Guid.CreateVersion7(), SessionStart);

        await NewRepository().AddAsync(booking, TestContext.Current.CancellationToken);

        var messages = await MessagesOfAsync(booking.Id);
        var message = messages.ShouldHaveSingleItem();
        message.Type.ShouldBe(nameof(Domain.Bookings.Events.BookingCreated));
        message.Status.ShouldBe(OutboxMessageDocument.PendingStatus);
        DomainEventSerializer.Deserialize(message.Type, message.Payload)
            .ShouldBeOfType<Domain.Bookings.Events.BookingCreated>().BookingId.ShouldBe(booking.Id);
        booking.DomainEvents.ShouldBeEmpty();
    }

    /// <summary>
    /// A booking rejected because its slot is taken leaves no outbox message behind: the event is published only if the
    /// change is committed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AddAsync_WithOverlappingSlot_StoresNoOutboxMessage()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var photographerId = Guid.CreateVersion7();
        var repository = NewRepository();
        await repository.AddAsync(NewBooking(photographerId, SessionStart), TestContext.Current.CancellationToken);
        var rejected = NewBooking(photographerId, SessionStart.AddHours(1));

        await Should.ThrowAsync<ConflictException>(() => repository.AddAsync(rejected, TestContext.Current.CancellationToken));

        (await MessagesOfAsync(rejected.Id)).ShouldBeEmpty();
        rejected.DomainEvents.ShouldNotBeEmpty();
    }

    /// <summary>
    /// Updating an active booking stores the new events in the outbox in the same write.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UpdateAsync_OfAnActiveBooking_StoresTheNewEventInTheOutbox()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = NewRepository();
        var created = NewBooking(Guid.CreateVersion7(), SessionStart);
        await repository.AddAsync(created, TestContext.Current.CancellationToken);
        var booking = (await repository.GetByIdAsync(created.Id, TestContext.Current.CancellationToken))!;
        booking.SignContract("María Pérez", "v1", Actor.Photographer, Channel.InPerson, Now);

        await repository.UpdateAsync(booking, TestContext.Current.CancellationToken);

        var types = (await MessagesOfAsync(booking.Id)).Select(message => message.Type).Order().ToList();
        types.ShouldBe([nameof(Domain.Bookings.Events.BookingCreated), nameof(Domain.Bookings.Events.ContractSigned)]);
        booking.DomainEvents.ShouldBeEmpty();
    }

    /// <summary>
    /// An update that loses the optimistic-concurrency race stores no message, so the event of a change that never
    /// happened is not published.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UpdateAsync_WithStaleVersion_StoresNoOutboxMessage()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = NewRepository();
        var created = NewBooking(Guid.CreateVersion7(), SessionStart);
        await repository.AddAsync(created, TestContext.Current.CancellationToken);
        var winner = (await repository.GetByIdAsync(created.Id, TestContext.Current.CancellationToken))!;
        var loser = (await repository.GetByIdAsync(created.Id, TestContext.Current.CancellationToken))!;
        winner.SignContract("María Pérez", "v1", Actor.Photographer, Channel.InPerson, Now);
        loser.Cancel(Actor.Photographer, null, Now);
        await repository.UpdateAsync(winner, TestContext.Current.CancellationToken);

        var exception = await Should.ThrowAsync<ConflictException>(() => repository.UpdateAsync(loser, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.ConcurrencyConflict);
        var types = (await MessagesOfAsync(created.Id)).Select(message => message.Type).ToList();
        types.ShouldNotContain(nameof(Domain.Bookings.Events.BookingCancelled));
    }

    /// <summary>
    /// A booking that leaves the active states stores its event and releases the slot in the same transaction.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UpdateAsync_OfAnExpiredBooking_StoresTheExpirationEvent()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = NewRepository();
        var created = NewBooking(Guid.CreateVersion7(), SessionStart);
        await repository.AddAsync(created, TestContext.Current.CancellationToken);
        var booking = (await repository.GetByIdAsync(created.Id, TestContext.Current.CancellationToken))!;
        booking.Expire(Now.AddHours(BookingPolicy.Default.TentativeHoldHours + 1));

        await repository.UpdateAsync(booking, TestContext.Current.CancellationToken);

        (await MessagesOfAsync(booking.Id)).Select(message => message.Type)
            .ShouldContain(nameof(Domain.Bookings.Events.BookingExpired));
    }

    /// <summary>
    /// The expiration query returns tentative bookings whose hold ended, and nothing else: not holds still running, not
    /// bookings with a proof of payment waiting for review, not bookings that are no longer tentative.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ListExpiredTentativeAsync_ReturnsOnlyDueTentativeBookingsWithoutPendingProof()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = NewRepository();
        var due = NewBooking(Guid.CreateVersion7(), SessionStart);
        var notDueYet = NewBooking(Guid.CreateVersion7(), SessionStart.AddDays(1), createdAt: Now.AddHours(40));
        var withPendingProof = NewBooking(Guid.CreateVersion7(), SessionStart.AddDays(2));
        withPendingProof.SubmitPaymentProof(Guid.CreateVersion7(), Money.Create(50_000m, "CRC"), PaymentMethod.SinpeMovil, "proof-1", Now);
        var cancelled = NewBooking(Guid.CreateVersion7(), SessionStart.AddDays(3));
        cancelled.Cancel(Actor.Photographer, null, Now);
        foreach (var booking in new[] { due, notDueYet, withPendingProof, cancelled })
        {
            await repository.AddAsync(booking, TestContext.Current.CancellationToken);
        }

        var listed = await repository.ListExpiredTentativeAsync(
            Now.AddHours(BookingPolicy.Default.TentativeHoldHours + 1),
            100,
            TestContext.Current.CancellationToken);

        var ids = listed.Select(booking => booking.Id).ToList();
        ids.ShouldContain(due.Id);
        ids.ShouldNotContain(notDueYet.Id);
        ids.ShouldNotContain(withPendingProof.Id);
        ids.ShouldNotContain(cancelled.Id);
    }

    /// <summary>
    /// Reads the outbox messages whose payload belongs to the booking.
    /// </summary>
    /// <param name="bookingId">Booking identifier.</param>
    /// <returns>The messages, in no particular order.</returns>
    private async Task<List<OutboxMessageDocument>> MessagesOfAsync(Guid bookingId)
    {
        var all = await fixture.Database
            .GetCollection<OutboxMessageDocument>(OutboxProcessor.CollectionName)
            .Find(FilterDefinition<OutboxMessageDocument>.Empty)
            .ToListAsync(TestContext.Current.CancellationToken);

        return [.. all.Where(message => message.Payload.Contains(bookingId.ToString(), StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>
    /// Creates a repository over the throwaway database.
    /// </summary>
    /// <returns>A new repository.</returns>
    private MongoBookingRepository NewRepository() => new(fixture.Database);

    /// <summary>
    /// Creates a tentative two-hour booking.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="start">Start of the session.</param>
    /// <param name="createdAt">Creation instant; defaults to <see cref="Now"/>.</param>
    /// <returns>The booking.</returns>
    private static Booking NewBooking(Guid photographerId, DateTimeOffset start, DateTimeOffset? createdAt = null) => Booking.Create(
        Guid.CreateVersion7(),
        photographerId,
        ClientContact.Create("María Pérez", "+506 8888-8888"),
        "Retrato familiar",
        Money.Create(100_000m, "CRC"),
        TimeSlot.Create(start, start.AddHours(2)),
        BookingPolicy.Default,
        createdAt ?? Now);
}
