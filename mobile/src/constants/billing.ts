/**
 * Values of the backend billing enums, kept as `as const` arrays like the booking enums: they validate responses at runtime
 * and define the matching types in `src/types/api/billing.ts`. Keep in sync with the backend.
 */

/** Every value of the backend `SettlementReason` enum. */
export const SETTLEMENT_REASONS = [
  'PhotographerCancelled',
  'TentativeCancelled',
  'ClientCancelledInTime',
  'ClientCancelledLate',
  'BookingExpired',
  'ClientAbsent',
] as const;

/** Every value of the backend `RetentionStatus` enum. */
export const RETENTION_STATUSES = ['None', 'Applied', 'Reversed'] as const;

/** Every value of the backend `RefundStatus` enum. */
export const REFUND_STATUSES = ['None', 'Pending', 'Completed', 'Voided'] as const;

/** Longest note the backend accepts when a refund is completed. */
export const MAX_REFUND_NOTE_LENGTH = 200;

/** Statuses of a booking whose money may need to be settled; the detail asks for the settlement only in these. */
export const SETTLED_BOOKING_STATUSES = ['Cancelled', 'Expired', 'ClientAbsent'] as const;
