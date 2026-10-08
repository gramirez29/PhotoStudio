/** Booking actions the app can perform today; the rest are listed as "coming soon" in the booking detail. */
export const APP_SUPPORTED_ACTIONS = ['Reschedule', 'Complete', 'MarkClientAbsent', 'RevertClientAbsent', 'Cancel'] as const;

/** Actions that open a screen to type a reason instead of acting right away. */
export const REASON_ACTIONS = ['Cancel', 'RevertClientAbsent'] as const;
