/** Booking actions the app can perform today; the rest are listed as "coming soon" in the booking detail. */
export const APP_SUPPORTED_ACTIONS = [
  'SignContract',
  'RecordInPersonPayment',
  'Reschedule',
  'Complete',
  'MarkClientAbsent',
  'RevertClientAbsent',
  'EditDetails',
  'Cancel',
] as const;

/**
 * Allowed actions the booking detail handles with a card of their own instead of a generic button, because they act on one
 * payment and not on the booking: verifying a payment proof, with its reject counterpart.
 */
export const CARD_HANDLED_ACTIONS = ['VerifyPayment', 'EditPackage'] as const;

/** Actions that open a screen to type a reason instead of acting right away. */
export const REASON_ACTIONS = ['Cancel', 'RevertClientAbsent'] as const;
