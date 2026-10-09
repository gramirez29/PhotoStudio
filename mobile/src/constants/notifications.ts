/**
 * Values of the backend notification enums, kept as `as const` arrays like the booking enums: they validate responses at
 * runtime and define the matching types in `src/types/api/notifications.ts`. Keep in sync with the backend.
 */

/** Every value of the backend `NotificationType` enum. */
export const NOTIFICATION_TYPES = ['SessionReminder', 'BalanceDue'] as const;
