import { router } from 'expo-router';
import type { ReasonAction } from '../types/utils/bookingActions.types';

/**
 * Opens the detail screen of a booking.
 * @param bookingId Booking identifier.
 */
export function openBooking(bookingId: string): void {
  router.push({ pathname: '/bookings/[id]', params: { id: bookingId } });
}

/** Opens the list of all the bookings, with its search and filters. */
export function openAllBookings(): void {
  router.push('/bookings');
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

/** Opens the list of delivered galleries whose space the photographer can free. */
export function openArchivableGalleries(): void {
  router.push('/archivable');
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

/** Opens the settings: the appearance of the app and the account. */
export function openSettings(): void {
  router.push('/settings');
}

/** Opens the inbox of notices. */
export function openNotifications(): void {
  router.push('/notifications');
}

/**
 * Opens the gallery of a session, where the photographer sees the selection and runs the steps of the delivery.
 * @param galleryId Gallery identifier.
 */
export function openGallery(galleryId: string): void {
  router.push({ pathname: '/galleries/[id]', params: { id: galleryId } });
}

/**
 * Opens the screen where the photographer changes the terms of a gallery: included photos, gift photos and the price of an extra.
 * @param galleryId Gallery identifier.
 */
export function openGalleryTerms(galleryId: string): void {
  router.push({ pathname: '/galleries/terms', params: { id: galleryId } });
}

/**
 * Opens the screen where the photographer writes why the selection of the client is reopened.
 * @param galleryId Gallery identifier.
 */
export function openGalleryReopen(galleryId: string): void {
  router.push({ pathname: '/galleries/reopen', params: { id: galleryId } });
}
