import { router } from 'expo-router';
import type { ReasonAction } from '../types/utils/bookingActions.types';

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
 * Opens the form to record the signature of the contract of a booking.
 * @param bookingId Booking identifier.
 */
export function openContract(bookingId: string): void {
  router.push({ pathname: '/bookings/contract', params: { id: bookingId } });
}

/**
 * Opens the form to record a payment received in person.
 * @param bookingId Booking identifier.
 */
export function openPayment(bookingId: string): void {
  router.push({ pathname: '/bookings/payment', params: { id: bookingId } });
}

/**
 * Opens the form to type the reason of an action on a booking.
 * @param bookingId Booking identifier.
 * @param action Action that needs the reason.
 */
export function openReason(bookingId: string, action: ReasonAction): void {
  router.push({ pathname: '/bookings/reason', params: { id: bookingId, action } });
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

/** Opens the inbox of notices. */
export function openNotifications(): void {
  router.push('/notifications');
}
