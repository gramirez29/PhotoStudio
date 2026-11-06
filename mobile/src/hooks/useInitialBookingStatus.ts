import { useLocalSearchParams } from 'expo-router';
import { BOOKING_STATUSES } from '../constants/booking';
import type { BookingStatus } from '../types/api/booking';

/**
 * Reads the status the list of bookings was opened with, for example from the home screen, which sends the photographer to the
 * tentative bookings that are about to expire.
 * @returns The status, or `null` when the list was opened without one or with a value that is not a status.
 */
export function useInitialBookingStatus(): BookingStatus | null {
  const { status } = useLocalSearchParams<{ status?: string }>();
  return BOOKING_STATUSES.find((option) => option === status) ?? null;
}
