using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Notifications;
using PhotoStudio.Infrastructure.Persistence;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Tests of <see cref="MongoNotificationRepository"/> against a real MongoDB replica set.
/// </summary>
/// <param name="fixture">Throwaway database shared by the tests of this class.</param>
public sealed class MongoNotificationRepositoryTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly NotificationDetails Details = new("María Pérez", "+50688888888", "Retrato", Now.AddDays(2), Money.Create(50_000m, "CRC"));

    /// <summary>
    /// Creates a scheduled reminder.
    /// </summary>
    /// <param name="photographerId">Owner.</param>
    /// <param name="bookingId">Booking.</param>
    /// <param name="sessionStart">Session start the reminder is for.</param>
    /// <param name="dueAt">Instant it becomes due.</param>
    /// <returns>The notification.</returns>
    private static Notification Reminder(Guid photographerId, Guid bookingId, DateTimeOffset sessionStart, DateTimeOffset dueAt) =>
        Notification.Schedule(
            photographerId, bookingId, NotificationType.SessionReminder, Notification.SessionReminderKey(bookingId, sessionStart), dueAt, Now);

    /// <summary>
    /// Creates the repository after making sure the indexes exist.
    /// </summary>
    /// <returns>The repository.</returns>
    private async Task<MongoNotificationRepository> NewRepositoryAsync()
    {
        await new MongoIndexInitializer(fixture.Database, NullLogger<MongoIndexInitializer>.Instance).StartAsync(TestContext.Current.CancellationToken);
        return new MongoNotificationRepository(fixture.Database);
    }

    /// <summary>
    /// Scheduling the same notice twice stores it once.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TryAddAsync_WithTheSameKey_StoresItOnce()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var photographer = Guid.CreateVersion7();
        var booking = Guid.CreateVersion7();
        var start = Now.AddDays(3);

        var first = await repository.TryAddAsync(Reminder(photographer, booking, start, Now.AddDays(2)), TestContext.Current.CancellationToken);
        var second = await repository.TryAddAsync(Reminder(photographer, booking, start, Now.AddDays(2)), TestContext.Current.CancellationToken);

        first.ShouldBeTrue();
        second.ShouldBeFalse();
    }

    /// <summary>
    /// Cancelling scheduled reminders keeps the one with the excepted key and does not touch delivered ones.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task CancelScheduledAsync_KeepsTheExceptedKeyAndDeliveredOnes()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var photographer = Guid.CreateVersion7();
        var booking = Guid.CreateVersion7();
        var oldStart = Now.AddDays(3);
        var newStart = Now.AddDays(5);
        var stale = Reminder(photographer, booking, oldStart, Now.AddDays(2));
        var current = Reminder(photographer, booking, newStart, Now.AddDays(4));
        var delivered = Reminder(photographer, booking, Now.AddDays(1), Now.AddHours(1));
        delivered.Deliver(Details, Now.AddHours(1));
        await repository.TryAddAsync(stale, TestContext.Current.CancellationToken);
        await repository.TryAddAsync(current, TestContext.Current.CancellationToken);
        await repository.TryAddAsync(delivered, TestContext.Current.CancellationToken);

        var cancelled = await repository.CancelScheduledAsync(
            booking, NotificationType.SessionReminder, current.DedupKey, Now, TestContext.Current.CancellationToken);

        cancelled.ShouldBe(1);
        (await repository.GetByIdAsync(stale.Id, TestContext.Current.CancellationToken))!.Status.ShouldBe(NotificationStatus.Cancelled);
        (await repository.GetByIdAsync(current.Id, TestContext.Current.CancellationToken))!.Status.ShouldBe(NotificationStatus.Scheduled);
        (await repository.GetByIdAsync(delivered.Id, TestContext.Current.CancellationToken))!.Status.ShouldBe(NotificationStatus.Delivered);
    }

    /// <summary>
    /// Only scheduled notifications whose time has come are listed, oldest first, up to the limit.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ListDueAsync_ReturnsOnlyDueScheduledOnesInOrder()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var photographer = Guid.CreateVersion7();
        var later = Reminder(photographer, Guid.CreateVersion7(), Now.AddDays(9), Now.AddHours(-1));
        var earlier = Reminder(photographer, Guid.CreateVersion7(), Now.AddDays(9), Now.AddHours(-5));
        var future = Reminder(photographer, Guid.CreateVersion7(), Now.AddDays(9), Now.AddHours(5));
        var done = Reminder(photographer, Guid.CreateVersion7(), Now.AddDays(9), Now.AddHours(-9));
        done.Cancel(Now);
        foreach (var notification in new[] { later, earlier, future, done })
        {
            await repository.TryAddAsync(notification, TestContext.Current.CancellationToken);
        }

        var due = await repository.ListDueAsync(Now, 100, TestContext.Current.CancellationToken);

        var mine = due.Where(notification => notification.PhotographerId == photographer).Select(notification => notification.Id).ToList();
        mine.ShouldBe([earlier.Id, later.Id]);
    }

    /// <summary>
    /// The inbox shows delivered notices of the photographer, newest first, with the unread count; marking all read zeroes it.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Inbox_ListsCountsAndMarksAsRead_PerPhotographer()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var mine = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        var older = Reminder(mine, Guid.CreateVersion7(), Now.AddDays(3), Now);
        var newer = Reminder(mine, Guid.CreateVersion7(), Now.AddDays(3), Now);
        var foreign = Reminder(other, Guid.CreateVersion7(), Now.AddDays(3), Now);
        var scheduled = Reminder(mine, Guid.CreateVersion7(), Now.AddDays(3), Now.AddDays(1));
        older.Deliver(Details, Now.AddMinutes(1));
        newer.Deliver(Details, Now.AddMinutes(2));
        foreign.Deliver(Details, Now.AddMinutes(3));
        foreach (var notification in new[] { older, newer, foreign, scheduled })
        {
            await repository.TryAddAsync(notification, TestContext.Current.CancellationToken);
        }

        var listed = await repository.ListDeliveredAsync(mine, 50, TestContext.Current.CancellationToken);
        var unread = await repository.CountUnreadAsync(mine, TestContext.Current.CancellationToken);
        var marked = await repository.MarkAllReadAsync(mine, Now.AddHours(1), TestContext.Current.CancellationToken);

        listed.Select(notification => notification.Id).ShouldBe([newer.Id, older.Id]);
        unread.ShouldBe(2);
        marked.ShouldBe(2);
        (await repository.CountUnreadAsync(mine, TestContext.Current.CancellationToken)).ShouldBe(0);
        (await repository.CountUnreadAsync(other, TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    /// <summary>
    /// The snapshot, including the amount owed, survives a round trip.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UpdateAsync_PersistsTheDeliveredSnapshot()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var notification = Reminder(Guid.CreateVersion7(), Guid.CreateVersion7(), Now.AddDays(3), Now);
        await repository.TryAddAsync(notification, TestContext.Current.CancellationToken);
        var stored = (await repository.GetByIdAsync(notification.Id, TestContext.Current.CancellationToken))!;

        stored.Deliver(Details, Now.AddMinutes(1));
        await repository.UpdateAsync(stored, TestContext.Current.CancellationToken);

        var reloaded = (await repository.GetByIdAsync(notification.Id, TestContext.Current.CancellationToken))!;
        reloaded.Status.ShouldBe(NotificationStatus.Delivered);
        reloaded.Details.ShouldBe(Details);
        reloaded.Version.ShouldBe(stored.Version + 1);
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
        var notification = Reminder(Guid.CreateVersion7(), Guid.CreateVersion7(), Now.AddDays(3), Now);
        await repository.TryAddAsync(notification, TestContext.Current.CancellationToken);
        var first = (await repository.GetByIdAsync(notification.Id, TestContext.Current.CancellationToken))!;
        var second = (await repository.GetByIdAsync(notification.Id, TestContext.Current.CancellationToken))!;
        first.Cancel(Now);
        await repository.UpdateAsync(first, TestContext.Current.CancellationToken);
        second.Deliver(Details, Now);

        await Should.ThrowAsync<ConflictException>(() => repository.UpdateAsync(second, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The collection has the retention (TTL) index that removes old notifications.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Indexes_IncludeTheRetentionTtl()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await NewRepositoryAsync();

        var result = await fixture.Database.RunCommandAsync<BsonDocument>(
            new BsonDocument("listIndexes", MongoNotificationRepository.CollectionName),
            cancellationToken: TestContext.Current.CancellationToken);

        var indexes = result["cursor"]["firstBatch"].AsBsonArray.Select(index => index.AsBsonDocument).ToList();
        var ttl = indexes.Single(index => index["name"] == "ix_notification_retention");
        ttl["expireAfterSeconds"].ToInt32().ShouldBe(0);
        indexes.Single(index => index["name"] == "ix_notification_dedup")["unique"].AsBoolean.ShouldBeTrue();
    }
}
