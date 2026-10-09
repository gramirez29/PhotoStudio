using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Notifications;

/// <summary>
/// What a delivered notification says: a snapshot of the booking taken at the moment the notification was delivered, so the
/// inbox keeps showing what was true then even if the booking changes later.
/// </summary>
/// <param name="ClientName">Name of the client.</param>
/// <param name="ClientPhone">Phone of the client, used to open a WhatsApp conversation.</param>
/// <param name="PackageName">Name of the booked package.</param>
/// <param name="SessionStart">Start of the session (UTC).</param>
/// <param name="Balance">Amount still owed; set only for <see cref="NotificationType.BalanceDue"/>.</param>
public sealed record NotificationDetails(string ClientName, string ClientPhone, string PackageName, DateTimeOffset SessionStart, Money? Balance);

/// <summary>
/// A notice for the photographer about one of their bookings. It is created <see cref="NotificationStatus.Scheduled"/> for a
/// future instant, and when that instant comes it is either delivered to the inbox (if it is still relevant) or cancelled.
/// It carries no text: it carries what happened and the data to act on it, and the app words it, so the backend holds no
/// user-facing language.
/// </summary>
public sealed class Notification : AggregateRoot<Guid>
{
    /// <summary>
    /// How long a notification is kept after its due instant before MongoDB removes it.
    /// </summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    /// <summary>
    /// Initializes a new instance of the <see cref="Notification"/> class. Use <see cref="Schedule"/> or <see cref="Restore"/>.
    /// </summary>
    private Notification(
        Guid id,
        long version,
        Guid photographerId,
        Guid bookingId,
        NotificationType type,
        string dedupKey,
        DateTimeOffset dueAt,
        NotificationStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? deliveredAt,
        DateTimeOffset? readAt,
        DateTimeOffset? cancelledAt,
        NotificationDetails? details)
        : base(id, version)
    {
        PhotographerId = photographerId;
        BookingId = bookingId;
        Type = type;
        DedupKey = dedupKey;
        DueAt = dueAt;
        Status = status;
        CreatedAt = createdAt;
        DeliveredAt = deliveredAt;
        ReadAt = readAt;
        CancelledAt = cancelledAt;
        Details = details;
    }

    /// <summary>
    /// Gets the photographer (tenant) the notification is for.
    /// </summary>
    public Guid PhotographerId { get; }

    /// <summary>
    /// Gets the booking the notification is about.
    /// </summary>
    public Guid BookingId { get; }

    /// <summary>
    /// Gets what the notification is about.
    /// </summary>
    public NotificationType Type { get; }

    /// <summary>
    /// Gets the key that makes scheduling idempotent: two notifications with the same key are the same notice. Events reach
    /// consumers at least once, so the same event can ask to schedule the same notice twice.
    /// </summary>
    public string DedupKey { get; }

    /// <summary>
    /// Gets the instant the notification becomes due (UTC).
    /// </summary>
    public DateTimeOffset DueAt { get; }

    /// <summary>
    /// Gets where the notification is in its life.
    /// </summary>
    public NotificationStatus Status { get; private set; }

    /// <summary>
    /// Gets the instant the notification was scheduled (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the instant the notification reached the inbox (UTC), or <see langword="null"/> before that.
    /// </summary>
    public DateTimeOffset? DeliveredAt { get; private set; }

    /// <summary>
    /// Gets the instant the photographer read it (UTC), or <see langword="null"/> while it is unread.
    /// </summary>
    public DateTimeOffset? ReadAt { get; private set; }

    /// <summary>
    /// Gets the instant the notification was dropped (UTC), or <see langword="null"/> if it was not.
    /// </summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>
    /// Gets what the notification says, or <see langword="null"/> until it is delivered.
    /// </summary>
    public NotificationDetails? Details { get; private set; }

    /// <summary>
    /// Gets the instant after which the notification can be removed from storage (UTC).
    /// </summary>
    public DateTimeOffset RetainUntil => DueAt + Retention;

    /// <summary>
    /// Builds the key that identifies a session reminder: one per booking and session start, so moving the session to another
    /// time makes it a different reminder.
    /// </summary>
    /// <param name="bookingId">Booking the reminder is about.</param>
    /// <param name="sessionStart">Start of the session the reminder is for.</param>
    /// <returns>The key.</returns>
    public static string SessionReminderKey(Guid bookingId, DateTimeOffset sessionStart) =>
        $"{NotificationType.SessionReminder}:{bookingId:N}:{sessionStart.UtcTicks}";

    /// <summary>
    /// Builds the key that identifies the balance notice of a booking: there is only one per booking.
    /// </summary>
    /// <param name="bookingId">Booking the notice is about.</param>
    /// <returns>The key.</returns>
    public static string BalanceDueKey(Guid bookingId) => $"{NotificationType.BalanceDue}:{bookingId:N}";

    /// <summary>
    /// Schedules a notification.
    /// </summary>
    /// <param name="photographerId">Photographer it is for.</param>
    /// <param name="bookingId">Booking it is about.</param>
    /// <param name="type">What it is about.</param>
    /// <param name="dedupKey">Key that makes scheduling idempotent (see <see cref="SessionReminderKey"/> and <see cref="BalanceDueKey"/>).</param>
    /// <param name="dueAt">Instant it becomes due.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The scheduled notification.</returns>
    /// <exception cref="DomainException">When an identifier or the key is missing.</exception>
    public static Notification Schedule(
        Guid photographerId,
        Guid bookingId,
        NotificationType type,
        string dedupKey,
        DateTimeOffset dueAt,
        DateTimeOffset now)
    {
        if (photographerId == Guid.Empty || bookingId == Guid.Empty || string.IsNullOrWhiteSpace(dedupKey))
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The photographer, the booking and the key of a notification are required.");
        }

        return new Notification(
            Guid.CreateVersion7(), 0, photographerId, bookingId, type, dedupKey, dueAt, NotificationStatus.Scheduled, now, null, null, null, null);
    }

    /// <summary>
    /// Rebuilds a notification from persisted data, without validating it again.
    /// </summary>
    /// <param name="id">Notification identifier.</param>
    /// <param name="version">Stored version.</param>
    /// <param name="photographerId">Photographer.</param>
    /// <param name="bookingId">Booking.</param>
    /// <param name="type">Type.</param>
    /// <param name="dedupKey">Idempotency key.</param>
    /// <param name="dueAt">Due instant.</param>
    /// <param name="status">Status.</param>
    /// <param name="createdAt">Scheduling instant.</param>
    /// <param name="deliveredAt">Delivery instant, if delivered.</param>
    /// <param name="readAt">Read instant, if read.</param>
    /// <param name="cancelledAt">Cancellation instant, if cancelled.</param>
    /// <param name="details">Delivered snapshot, if delivered.</param>
    /// <returns>The restored notification.</returns>
    public static Notification Restore(
        Guid id,
        long version,
        Guid photographerId,
        Guid bookingId,
        NotificationType type,
        string dedupKey,
        DateTimeOffset dueAt,
        NotificationStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? deliveredAt,
        DateTimeOffset? readAt,
        DateTimeOffset? cancelledAt,
        NotificationDetails? details) =>
        new(id, version, photographerId, bookingId, type, dedupKey, dueAt, status, createdAt, deliveredAt, readAt, cancelledAt, details);

    /// <summary>
    /// Indicates whether the notification is due: still scheduled and its instant has come.
    /// </summary>
    /// <param name="now">Instant to evaluate.</param>
    /// <returns><see langword="true"/> when it should be delivered or cancelled now.</returns>
    public bool IsDueAt(DateTimeOffset now) => Status == NotificationStatus.Scheduled && DueAt <= now;

    /// <summary>
    /// Puts the notification in the inbox, with the snapshot of what it says.
    /// </summary>
    /// <param name="details">What the notification says.</param>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the notification is not scheduled.</exception>
    public void Deliver(NotificationDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        EnsureScheduled(nameof(Deliver));

        Details = details;
        DeliveredAt = now;
        Status = NotificationStatus.Delivered;
    }

    /// <summary>
    /// Drops a notification that stopped being relevant before its time came.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the notification is not scheduled.</exception>
    public void Cancel(DateTimeOffset now)
    {
        EnsureScheduled(nameof(Cancel));

        CancelledAt = now;
        Status = NotificationStatus.Cancelled;
    }

    /// <summary>
    /// Marks a delivered notification as read. Reading one that is already read changes nothing.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the notification was not delivered, so there is nothing to read.</exception>
    public void MarkRead(DateTimeOffset now)
    {
        if (Status != NotificationStatus.Delivered)
        {
            throw new DomainException(DomainErrorCodes.InvalidTransition, $"A notification that is {Status} cannot be read.");
        }

        ReadAt ??= now;
    }

    /// <summary>
    /// Fails unless the notification is still scheduled.
    /// </summary>
    /// <param name="operation">Operation being attempted, for the message.</param>
    /// <exception cref="DomainException">When the notification is not scheduled.</exception>
    private void EnsureScheduled(string operation)
    {
        if (Status != NotificationStatus.Scheduled)
        {
            throw new DomainException(DomainErrorCodes.InvalidTransition, $"{operation} is not allowed on a notification that is {Status}.");
        }
    }
}
