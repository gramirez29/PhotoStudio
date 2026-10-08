import { router } from 'expo-router';

/**
 * Opens the detail screen of a booking.
 * @param bookingId Booking identifier.
 */
export function openBooking(bookingId: string): void {
  router.push({ pathname: '/bookings/[id]', params: { id: bookingId } });
}

/** Opens the create-booking form. */
export function openNewBooking(): void {
  router.push('/bookings/new');
}

/**
 * Opens the reschedule form of a booking.
 * @param bookingId Booking identifier.
 */
export function openReschedule(bookingId: string): void {
  router.push({ pathname: '/bookings/reschedule', params: { id: bookingId } });
}

/**
 * Replaces the current screen with the detail of a booking, so going back skips the screen that was replaced.
 * @param bookingId Booking identifier.
 */
export function replaceWithBooking(bookingId: string): void {
  router.replace({ pathname: '/bookings/[id]', params: { id: bookingId } });
}

/** Goes back to the previous screen. */
export function goBack(): void {
  router.back();
}
