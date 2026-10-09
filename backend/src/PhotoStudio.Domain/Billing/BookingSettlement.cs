using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Billing;

/// <summary>
/// Who the money is owed to and for which session, copied when the settlement is opened so the list of refunds shows it
/// without reading the booking.
/// </summary>
/// <param name="ClientName">Name of the client.</param>
/// <param name="ClientPhone">Phone of the client, to contact them about the refund.</param>
/// <param name="PackageName">Name of the booked package.</param>
/// <param name="SessionStart">Start of the session (UTC).</param>
public sealed record SettlementSnapshot(string ClientName, string ClientPhone, string PackageName, DateTimeOffset SessionStart);

/// <summary>
/// The money side of a booking that ended without delivering its session: what the photographer keeps and what still has to
/// go back to the client. There is one per booking. A payment is never edited or deleted; giving money back is recorded here
/// as a refund, which the photographer marks as done once they transfer it.
/// <para>
/// The settlement follows the booking: it is recalculated while no money has been given back, and once the refund is
/// completed it is final (a real transfer cannot be undone from here).
/// </para>
/// </summary>
public sealed class BookingSettlement : AggregateRoot<Guid>
{
    /// <summary>
    /// Longest note accepted when completing a refund.
    /// </summary>
    public const int MaxNoteLength = 200;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingSettlement"/> class. Use <see cref="Open"/> or <see cref="Restore"/>.
    /// </summary>
    private BookingSettlement(
        Guid id,
        long version,
        Guid photographerId,
        Guid bookingId,
        SettlementSnapshot snapshot,
        SettlementReason reason,
        Money totalPaid,
        Money retained,
        RetentionStatus retentionStatus,
        Money refundAmount,
        RefundStatus refundStatus,
        PaymentMethod? refundMethod,
        string? refundNote,
        DateTimeOffset? refundCompletedAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : base(id, version)
    {
        PhotographerId = photographerId;
        BookingId = bookingId;
        Snapshot = snapshot;
        Reason = reason;
        TotalPaid = totalPaid;
        Retained = retained;
        RetentionStatus = retentionStatus;
        RefundAmount = refundAmount;
        RefundStatus = refundStatus;
        RefundMethod = refundMethod;
        RefundNote = refundNote;
        RefundCompletedAt = refundCompletedAt;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Gets the photographer (tenant) the settlement belongs to.
    /// </summary>
    public Guid PhotographerId { get; }

    /// <summary>
    /// Gets the booking the settlement is about.
    /// </summary>
    public Guid BookingId { get; }

    /// <summary>
    /// Gets who the money is owed to and for which session.
    /// </summary>
    public SettlementSnapshot Snapshot { get; }

    /// <summary>
    /// Gets why money is kept or returned.
    /// </summary>
    public SettlementReason Reason { get; private set; }

    /// <summary>
    /// Gets the sum of the verified payments the settlement was calculated from.
    /// </summary>
    public Money TotalPaid { get; private set; }

    /// <summary>
    /// Gets the part of the deposit the photographer keeps.
    /// </summary>
    public Money Retained { get; private set; }

    /// <summary>
    /// Gets the state of the retention.
    /// </summary>
    public RetentionStatus RetentionStatus { get; private set; }

    /// <summary>
    /// Gets the amount that goes back to the client.
    /// </summary>
    public Money RefundAmount { get; private set; }

    /// <summary>
    /// Gets the state of the refund.
    /// </summary>
    public RefundStatus RefundStatus { get; private set; }

    /// <summary>
    /// Gets how the money was given back, once the refund is completed.
    /// </summary>
    public PaymentMethod? RefundMethod { get; private set; }

    /// <summary>
    /// Gets the note the photographer left when completing the refund (for example the SINPE reference), if any.
    /// </summary>
    public string? RefundNote { get; private set; }

    /// <summary>
    /// Gets the instant the refund was completed (UTC), or <see langword="null"/> while it is not.
    /// </summary>
    public DateTimeOffset? RefundCompletedAt { get; private set; }

    /// <summary>
    /// Gets the instant the settlement was opened (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the instant the settlement last changed (UTC).
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Opens the settlement of a booking with the outcome calculated for it.
    /// </summary>
    /// <param name="photographerId">Photographer the booking belongs to.</param>
    /// <param name="bookingId">Booking being settled.</param>
    /// <param name="snapshot">Who the money is owed to and for which session.</param>
    /// <param name="outcome">What the booking owes.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The settlement.</returns>
    /// <exception cref="DomainException">When an identifier is missing.</exception>
    public static BookingSettlement Open(
        Guid photographerId,
        Guid bookingId,
        SettlementSnapshot snapshot,
        SettlementOutcome outcome,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(outcome);

        if (photographerId == Guid.Empty || bookingId == Guid.Empty)
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The photographer and the booking of a settlement are required.");
        }

        return new BookingSettlement(
            Guid.CreateVersion7(),
            0,
            photographerId,
            bookingId,
            snapshot,
            outcome.Reason,
            outcome.TotalPaid,
            outcome.Retained,
            StatusOfRetention(outcome),
            outcome.Refund,
            StatusOfRefund(outcome),
            null,
            null,
            null,
            now,
            now);
    }

    /// <summary>
    /// Rebuilds a settlement from persisted data, without validating it again.
    /// </summary>
    /// <param name="id">Settlement identifier.</param>
    /// <param name="version">Stored version.</param>
    /// <param name="photographerId">Photographer.</param>
    /// <param name="bookingId">Booking.</param>
    /// <param name="snapshot">Snapshot of the client and the session.</param>
    /// <param name="reason">Reason.</param>
    /// <param name="totalPaid">Sum of the verified payments.</param>
    /// <param name="retained">Retained amount.</param>
    /// <param name="retentionStatus">Retention state.</param>
    /// <param name="refundAmount">Amount owed back.</param>
    /// <param name="refundStatus">Refund state.</param>
    /// <param name="refundMethod">How the refund was paid, if completed.</param>
    /// <param name="refundNote">Note left when completing the refund.</param>
    /// <param name="refundCompletedAt">Instant the refund was completed.</param>
    /// <param name="createdAt">Instant the settlement was opened.</param>
    /// <param name="updatedAt">Instant the settlement last changed.</param>
    /// <returns>The restored settlement.</returns>
    public static BookingSettlement Restore(
        Guid id,
        long version,
        Guid photographerId,
        Guid bookingId,
        SettlementSnapshot snapshot,
        SettlementReason reason,
        Money totalPaid,
        Money retained,
        RetentionStatus retentionStatus,
        Money refundAmount,
        RefundStatus refundStatus,
        PaymentMethod? refundMethod,
        string? refundNote,
        DateTimeOffset? refundCompletedAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new(
            id,
            version,
            photographerId,
            bookingId,
            snapshot,
            reason,
            totalPaid,
            retained,
            retentionStatus,
            refundAmount,
            refundStatus,
            refundMethod,
            refundNote,
            refundCompletedAt,
            createdAt,
            updatedAt);

    /// <summary>
    /// Brings the settlement in line with a new outcome of the booking (for example, it was cancelled after an absence mark
    /// was reverted). Does nothing once the refund is completed: that money already moved.
    /// </summary>
    /// <param name="outcome">What the booking owes now.</param>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when something changed and the settlement must be saved.</returns>
    public bool Apply(SettlementOutcome outcome, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        if (RefundStatus == RefundStatus.Completed)
        {
            return false;
        }

        var retentionStatus = StatusOfRetention(outcome);
        var refundStatus = StatusOfRefund(outcome);
        if (Reason == outcome.Reason
            && TotalPaid == outcome.TotalPaid
            && Retained == outcome.Retained
            && RetentionStatus == retentionStatus
            && RefundAmount == outcome.Refund
            && RefundStatus == refundStatus)
        {
            return false;
        }

        Reason = outcome.Reason;
        TotalPaid = outcome.TotalPaid;
        Retained = outcome.Retained;
        RetentionStatus = retentionStatus;
        RefundAmount = outcome.Refund;
        RefundStatus = refundStatus;
        UpdatedAt = now;
        return true;
    }

    /// <summary>
    /// Undoes the settlement because the booking is active again (the absence mark was reverted): the retention is reversed
    /// and a refund that was not paid yet stops being owed. Does nothing once the refund is completed.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when something changed and the settlement must be saved.</returns>
    public bool Void(DateTimeOffset now)
    {
        if (RefundStatus == RefundStatus.Completed)
        {
            return false;
        }

        var changed = false;
        if (RetentionStatus == RetentionStatus.Applied)
        {
            RetentionStatus = RetentionStatus.Reversed;
            changed = true;
        }

        if (RefundStatus == RefundStatus.Pending)
        {
            RefundStatus = RefundStatus.Voided;
            changed = true;
        }

        if (changed)
        {
            UpdatedAt = now;
        }

        return changed;
    }

    /// <summary>
    /// Records that the photographer gave the money back. Final: the refund cannot change afterwards.
    /// </summary>
    /// <param name="method">How the money was given back: cash or SINPE Móvil.</param>
    /// <param name="note">Optional note, for example the SINPE reference.</param>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When there is no pending refund, the method is not accepted or the note is too long.</exception>
    public void CompleteRefund(PaymentMethod method, string? note, DateTimeOffset now)
    {
        if (RefundStatus != RefundStatus.Pending)
        {
            throw new DomainException(DomainErrorCodes.InvalidTransition, $"There is no pending refund to complete (it is {RefundStatus}).");
        }

        if (method is not (PaymentMethod.Cash or PaymentMethod.SinpeMovil))
        {
            throw new DomainException(DomainErrorCodes.InvalidPaymentMethod, "A refund can be given back in cash or by SINPE Móvil.");
        }

        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed is { Length: > MaxNoteLength })
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, $"The note cannot be longer than {MaxNoteLength} characters.");
        }

        RefundStatus = RefundStatus.Completed;
        RefundMethod = method;
        RefundNote = trimmed;
        RefundCompletedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// Gets the retention state an outcome implies.
    /// </summary>
    /// <param name="outcome">Outcome to evaluate.</param>
    /// <returns><see cref="RetentionStatus.Applied"/> when something is retained, otherwise <see cref="RetentionStatus.None"/>.</returns>
    private static RetentionStatus StatusOfRetention(SettlementOutcome outcome) =>
        outcome.Retained.Amount > 0m ? RetentionStatus.Applied : RetentionStatus.None;

    /// <summary>
    /// Gets the refund state an outcome implies.
    /// </summary>
    /// <param name="outcome">Outcome to evaluate.</param>
    /// <returns><see cref="RefundStatus.Pending"/> when money goes back, otherwise <see cref="RefundStatus.None"/>.</returns>
    private static RefundStatus StatusOfRefund(SettlementOutcome outcome) =>
        outcome.Refund.Amount > 0m ? RefundStatus.Pending : RefundStatus.None;
}
