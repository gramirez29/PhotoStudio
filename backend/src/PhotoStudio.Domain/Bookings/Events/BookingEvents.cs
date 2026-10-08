using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Bookings.Events;

/// <summary>Raised when the photographer creates a booking (B1).</summary>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="PhotographerId">Photographer identifier.</param>
/// <param name="SessionStart">Start of the session (UTC).</param>
/// <param name="ExpiresAt">Instant the tentative hold ends (UTC).</param>
/// <param name="OccurredAt">Instant of the event (UTC).</param>
public sealed record BookingCreated(Guid BookingId, Guid PhotographerId, DateTimeOffset SessionStart, DateTimeOffset ExpiresAt, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Raised when the contract is signed (B2).</summary>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="TemplateVersion">Signed template version.</param>
/// <param name="Channel">Channel of the signature.</param>
/// <param name="OccurredAt">Instant of the event (UTC).</param>
public sealed record ContractSigned(Guid BookingId, string TemplateVersion, Channel Channel, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Raised when the client uploads a proof of payment (B3).</summary>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="PaymentId">Payment identifier.</param>
/// <param name="Amount">Declared amount.</param>
/// <param name="Currency">Currency code.</param>
/// <param name="OccurredAt">Instant of the event (UTC).</param>
public sealed record PaymentSubmitted(Guid BookingId, Guid PaymentId, decimal Amount, string Currency, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Raised when a payment becomes verified, either after review or because it was received in person (B4, B5).</summary>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="PaymentId">Payment identifier.</param>
/// <param name="Amount">Verified amount.</param>
/// <param name="Currency">Currency code.</param>
/// <param name="Method">Payment method.</param>
/// <param name="Channel">Channel of the payment.</param>
/// <param name="OccurredAt">Instant of the event (UTC).</param>
public sealed record PaymentVerified(Guid BookingId, Guid PaymentId, decimal Amount, string Currency, PaymentMethod Method, Channel Channel, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Raised when the photographer rejects a proof of payment (B4).</summary>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="PaymentId">Payment identifier.</param>
/// <param name="Reason">Rejection reason.</param>
/// <param name="OccurredAt">Instant of the event (UTC).</param>
public sealed record PaymentRejected(Guid BookingId, Guid PaymentId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Raised when the booking becomes confirmed (B6).</summary>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="PhotographerId">Photographer identifier.</param>
/// <param name="SessionStart">Start of the session (UTC).</param>
/// <param name="OccurredAt">Instant of the event (UTC).</param>
public sealed record BookingConfirmed(Guid BookingId, Guid PhotographerId, DateTimeOffset SessionStart, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Raised when a tentative booking expires (B7).</summary>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="OccurredAt">Instant of the event (UTC).</param>
public sealed record BookingExpired(Guid BookingId, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Raised when a booking is cancelled (B8, B10).</summary>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="CancelledBy">Who cancelled.</param>
/// <param name="WasConfirmed">Whether the booking was confirmed, so the policy applies.</param>
/// <param name="HoursBeforeSession">Notice given, in hours before the session start.</param>
/// <param name="Reason">Cancellation reason, if any.</param>
/// <param name="OccurredAt">Instant of the event (UTC).</param>
public sealed record BookingCancelled(Guid BookingId, Actor CancelledBy, bool WasConfirmed, double HoursBeforeSession, string? Reason, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Raised when a confirmed booking moves to another slot (B9).</summary>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="PreviousStart">Previous session start (UTC).</param>
/// <param name="NewStart">New session start (UTC).</param>
/// <param name="RequestedBy">Who requested the change.</param>
/// <param name="OccurredAt">Instant of the event (UTC).</param>
public sealed record BookingRescheduled(Guid BookingId, DateTimeOffset PreviousStart, DateTimeOffset NewStart, Actor RequestedBy, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Raised when the session is completed (B11). The gallery module creates its aggregate from it.</summary>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="PhotographerId">Photographer identifier.</param>
/// <param name="OccurredAt">Instant of the event (UTC).</param>
public sealed record SessionCompleted(Guid BookingId, Guid PhotographerId, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Raised when the photographer marks the client absent (B12).</summary>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="OccurredAt">Instant of the event (UTC).</param>
public sealed record ClientMarkedAbsent(Guid BookingId, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Raised when a client-absent mark is reverted (B13).</summary>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="Reason">Why the client-absent mark was reverted.</param>
/// <param name="OccurredAt">Instant of the event (UTC).</param>
public sealed record ClientAbsenceReverted(Guid BookingId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;
