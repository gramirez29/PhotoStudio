/**
 * Values of the backend enums. The API serializes them as strings (JsonStringEnumConverter), so the app keeps them as
 * `as const` arrays: they validate responses at runtime and, in `src/types/api/booking.ts`, define the matching types.
 * Keep in sync with the backend.
 */

/** Every value of the backend `BookingStatus` enum. */
export const BOOKING_STATUSES = ['Tentative', 'Confirmed', 'Completed', 'Cancelled', 'Expired', 'ClientAbsent'] as const;

/** Every value of the backend `PaymentMethod` enum. */
export const PAYMENT_METHODS = ['SinpeMovil', 'Cash', 'Card'] as const;

/** Every value of the backend `PaymentStatus` enum. */
export const PAYMENT_STATUSES = ['PendingVerification', 'Verified', 'Rejected'] as const;

/** Every value of the backend `Channel` enum. */
export const CHANNELS = ['Portal', 'PhotographerApp', 'InPerson', 'External'] as const;

/** Every value of the backend `BookingAction` enum. */
export const BOOKING_ACTIONS = [
  'SignContract',
  'SubmitPaymentProof',
  'VerifyPayment',
  'RecordInPersonPayment',
  'Reschedule',
  'Cancel',
  'Complete',
  'MarkClientAbsent',
  'RevertClientAbsent',
] as const;
