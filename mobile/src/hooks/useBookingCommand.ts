import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { executeBookingCommand } from '../api/bookingCommands';
import { bookingsApi } from '../api/client';
import type { BookingResponse } from '../types/api/booking';
import type { BookingCommand } from '../types/hooks/useBookingCommand.types';
import { bookingQueryKey } from './useBooking';
import { bookingsQueryKey } from './useBookings';

/**
 * Mutation that performs an action on a booking (cancel, complete, mark absent, revert). On success it replaces the
 * cached detail with the response and refreshes the photographer's list, so every screen shows the new status. It never
 * retries: repeating an action could fail on a transition that already happened.
 * @param bookingId Booking the actions are performed on.
 * @returns The TanStack Query mutation.
 */
export function useBookingCommand(bookingId: string): UseMutationResult<BookingResponse, Error, BookingCommand> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (command: BookingCommand) => executeBookingCommand(bookingsApi, bookingId, command),
    onSuccess: async (booking) => {
      queryClient.setQueryData(bookingQueryKey(booking.id), booking);
      await queryClient.invalidateQueries({ queryKey: bookingsQueryKey(booking.photographerId) });
    },
  });
}
