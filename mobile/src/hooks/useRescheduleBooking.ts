import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { bookingsApi } from '../api/client';
import type { BookingResponse, RescheduleBookingRequest } from '../types/api/booking';
import { bookingQueryKey } from './useBooking';
import { bookingsQueryKey } from './useBookings';

/**
 * Mutation that moves a booking to another slot. On success it replaces the cached detail with the response and refreshes
 * the photographer's list, so both screens show the new date. It never retries: a repeated request could race itself.
 * @param bookingId Booking to move.
 * @returns The TanStack Query mutation.
 */
export function useRescheduleBooking(bookingId: string): UseMutationResult<BookingResponse, Error, RescheduleBookingRequest> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: RescheduleBookingRequest) => bookingsApi.rescheduleBooking(bookingId, request),
    onSuccess: async (booking) => {
      queryClient.setQueryData(bookingQueryKey(booking.id), booking);
      await queryClient.invalidateQueries({ queryKey: bookingsQueryKey(booking.photographerId) });
    },
  });
}
