using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Bookings;

/// <summary>
/// Payment recorded against a booking. Payments are never edited or deleted; a rejected proof stays as evidence.
/// </summary>
public sealed class Payment
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Payment"/> class. Use the static factory methods, which validate the values.
    /// </summary>
    private Payment(
        Guid id,
        Money amount,
        PaymentMethod method,
        Channel channel,
        Actor recordedBy,
        PaymentStatus status,
        string idempotencyKey,
        DateTimeOffset recordedAt,
        DateTimeOffset? resolvedAt,
        string? rejectionReason)
    {
        Id = id;
        Amount = amount;
        Method = method;
        Channel = channel;
        RecordedBy = recordedBy;
        Status = status;
        IdempotencyKey = idempotencyKey;
        RecordedAt = recordedAt;
        ResolvedAt = resolvedAt;
        RejectionReason = rejectionReason;
    }

    /// <summary>Gets the payment identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the paid amount.</summary>
    public Money Amount { get; }

    /// <summary>Gets the payment method.</summary>
    public PaymentMethod Method { get; }

    /// <summary>Gets the channel through which the payment was recorded.</summary>
    public Channel Channel { get; }

    /// <summary>Gets who recorded the payment.</summary>
    public Actor RecordedBy { get; }

    /// <summary>Gets the verification status.</summary>
    public PaymentStatus Status { get; private set; }

    /// <summary>Gets the key that makes retries (for example from an offline queue) safe.</summary>
    public string IdempotencyKey { get; }

    /// <summary>Gets the instant the payment was recorded (UTC).</summary>
    public DateTimeOffset RecordedAt { get; }

    /// <summary>Gets the instant the payment was verified or rejected (UTC), if it was.</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>Gets the reason given when the proof was rejected.</summary>
    public string? RejectionReason { get; private set; }

    /// <summary>
    /// Rebuilds a payment from persisted data.
    /// </summary>
    /// <param name="id">Payment identifier.</param>
    /// <param name="amount">Paid amount.</param>
    /// <param name="method">Payment method.</param>
    /// <param name="channel">Channel of the payment.</param>
    /// <param name="recordedBy">Actor that recorded the payment.</param>
    /// <param name="status">Verification status.</param>
    /// <param name="idempotencyKey">Idempotency key.</param>
    /// <param name="recordedAt">Instant the payment was recorded.</param>
    /// <param name="resolvedAt">Instant the payment was verified or rejected.</param>
    /// <param name="rejectionReason">Rejection reason, if any.</param>
    /// <returns>The restored payment.</returns>
    public static Payment Restore(
        Guid id,
        Money amount,
        PaymentMethod method,
        Channel channel,
        Actor recordedBy,
        PaymentStatus status,
        string idempotencyKey,
        DateTimeOffset recordedAt,
        DateTimeOffset? resolvedAt,
        string? rejectionReason) =>
        new(id, amount, method, channel, recordedBy, status, idempotencyKey, recordedAt, resolvedAt, rejectionReason);

    /// <summary>
    /// Creates a payment proof submitted by the client that still needs the photographer's verification.
    /// </summary>
    /// <param name="id">Payment identifier.</param>
    /// <param name="amount">Declared amount.</param>
    /// <param name="method">Payment method.</param>
    /// <param name="idempotencyKey">Idempotency key.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>A payment pending verification.</returns>
    internal static Payment CreatePending(Guid id, Money amount, PaymentMethod method, string idempotencyKey, DateTimeOffset now) =>
        new(id, amount, method, Channel.Portal, Actor.Client, PaymentStatus.PendingVerification, idempotencyKey, now, null, null);

    /// <summary>
    /// Creates a payment received face to face by the photographer. It is verified on creation.
    /// </summary>
    /// <param name="id">Payment identifier.</param>
    /// <param name="amount">Received amount.</param>
    /// <param name="method">Payment method.</param>
    /// <param name="idempotencyKey">Idempotency key.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>A verified payment.</returns>
    internal static Payment CreateVerifiedInPerson(Guid id, Money amount, PaymentMethod method, string idempotencyKey, DateTimeOffset now) =>
        new(id, amount, method, Channel.InPerson, Actor.Photographer, PaymentStatus.Verified, idempotencyKey, now, now, null);

    /// <summary>
    /// Marks a pending proof as verified.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the payment is not pending verification.</exception>
    internal void Verify(DateTimeOffset now)
    {
        EnsurePending();
        Status = PaymentStatus.Verified;
        ResolvedAt = now;
    }

    /// <summary>
    /// Marks a pending proof as rejected.
    /// </summary>
    /// <param name="reason">Why the proof was rejected.</param>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the payment is not pending verification.</exception>
    internal void Reject(string reason, DateTimeOffset now)
    {
        EnsurePending();
        Status = PaymentStatus.Rejected;
        ResolvedAt = now;
        RejectionReason = reason;
    }

    /// <summary>
    /// Ensures the payment is still pending verification.
    /// </summary>
    /// <exception cref="DomainException">When the payment was already verified or rejected.</exception>
    private void EnsurePending()
    {
        if (Status != PaymentStatus.PendingVerification)
        {
            throw new DomainException(DomainErrorCodes.InvalidPaymentStatus, $"Payment {Id} is {Status} and cannot change anymore.");
        }
    }
}
