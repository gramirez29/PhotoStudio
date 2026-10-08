import type { BookingResponse } from '../types/api/booking';
import type { BookingsApi } from '../types/api/bookingsApi';
import type { BookingCommand } from '../types/hooks/useBookingCommand.types';

/**
 * Sends an action on a booking to the endpoint that handles it.
 * @param api Bookings API.
 * @param bookingId Booking the action is performed on.
 * @param command Action to perform.
 * @returns The booking after the action.
 */
export function executeBookingCommand(
  api: BookingsApi,
  bookingId: string,
  command: BookingCommand,
): Promise<BookingResponse> {
  switch (command.kind) {
    case 'cancel':
      return api.cancelBooking(bookingId, command.reason === null ? {} : { reason: command.reason });
    case 'complete':
      return api.completeBooking(bookingId);
    case 'markClientAbsent':
      return api.markClientAbsent(bookingId);
    case 'revertClientAbsent':
      return api.revertClientAbsent(bookingId, { reason: command.reason });
  }
}
