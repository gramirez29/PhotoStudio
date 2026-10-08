import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { bookingsApi } from '../api/client';
import type { BookingResponse, CreateBookingRequest } from '../types/api/booking';
import { bookingQueryKey } from './useBooking';
import { bookingsQueryKey } from './useBookings';

/**
 * Mutation that creates a booking. On success it seeds the detail cache, so the detail screen opens without another
 * request, and refreshes the photographer's list. It never retries: creating is not idempotent.
 * @returns The TanStack Query mutation.
 */
export function useCreateBooking(): UseMutationResult<BookingResponse, Error, CreateBookingRequest> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CreateBookingRequest) => bookingsApi.createBooking(request),
    onSuccess: async (booking, request) => {
      queryClient.setQueryData(bookingQueryKey(booking.id), booking);
      await queryClient.invalidateQueries({ queryKey: bookingsQueryKey(request.photographerId) });
    },
  });
}
