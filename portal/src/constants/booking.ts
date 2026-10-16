/** Statuses of a booking, as the backend names them. */
export const BOOKING_STATUSES = ['Tentative', 'Confirmed', 'Completed', 'Cancelled', 'Expired', 'ClientAbsent'] as const;

/** Verification statuses of a payment, as the backend names them. */
export const PAYMENT_STATUSES = ['PendingVerification', 'Verified', 'Rejected'] as const;
