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
/**
 * Opens the screen that corrects the data of a booking.
 * @param bookingId Booking to edit.
 */
export function openEditBooking(bookingId: string): void {
  router.push({ pathname: '/bookings/edit', params: { id: bookingId } });
}

/**
 * Opens the screen that records the signature of the contract of a booking.
 * @param bookingId Booking whose contract is signed.
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
 * Opens the form to type the reason for rejecting a payment proof.
 * @param bookingId Booking identifier.
 * @param paymentId Payment proof being rejected.
 */
export function openRejectPayment(bookingId: string, paymentId: string): void {
  router.push({ pathname: '/bookings/reject-payment', params: { id: bookingId, paymentId } });
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

/** Opens the list of refunds the photographer still has to give back. */
export function openRefunds(): void {
  router.push('/refunds');
}

/**
 * Opens the form to record that the refund of a booking was given back.
 * @param bookingId Booking identifier.
 */
export function openRefund(bookingId: string): void {
  router.push({ pathname: '/bookings/refund', params: { id: bookingId } });
}

/** Opens the form to change the password. */
export function openChangePassword(): void {
  router.push('/change-password');
}

/** Opens the inbox of notices. */
export function openNotifications(): void {
  router.push('/notifications');
}
