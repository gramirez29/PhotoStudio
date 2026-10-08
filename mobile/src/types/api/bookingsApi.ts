import type {
  BookingResponse,
  BookingSummaryResponse,
  CancelBookingRequest,
  CreateBookingRequest,
  RescheduleBookingRequest,
  RevertClientAbsentRequest,
} from './booking';

/** Booking endpoints used by the photographer app. */
export interface BookingsApi {
  /**
   * Creates a tentative booking.
   * @param request Booking data.
   * @param signal Optional signal to cancel the request.
   * @returns The created booking.
   */
  createBooking(request: CreateBookingRequest, signal?: AbortSignal): Promise<BookingResponse>;

  /**
   * Moves a confirmed booking to another slot.
   * @param id Booking identifier.
   * @param request New session start and end.
   * @param signal Optional signal to cancel the request.
   * @returns The updated booking.
   */
  rescheduleBooking(id: string, request: RescheduleBookingRequest, signal?: AbortSignal): Promise<BookingResponse>;

  /**
   * Cancels a booking. The reason is mandatory once the booking is confirmed.
   * @param id Booking identifier.
   * @param request Optional reason.
   * @param signal Optional signal to cancel the request.
   * @returns The updated booking.
   */
  cancelBooking(id: string, request: CancelBookingRequest, signal?: AbortSignal): Promise<BookingResponse>;

  /**
   * Marks a confirmed booking as completed once its session has started.
   * @param id Booking identifier.
   * @param signal Optional signal to cancel the request.
   * @returns The updated booking.
   */
  completeBooking(id: string, signal?: AbortSignal): Promise<BookingResponse>;

  /**
   * Records that the client did not show up, once the tolerance of the policy has passed.
   * @param id Booking identifier.
   * @param signal Optional signal to cancel the request.
   * @returns The updated booking.
   */
  markClientAbsent(id: string, signal?: AbortSignal): Promise<BookingResponse>;

  /**
   * Reverts a client-absent mark made by mistake, within the window of the policy.
   * @param id Booking identifier.
   * @param request Reason for the reversal.
   * @param signal Optional signal to cancel the request.
   * @returns The updated booking.
   */
  revertClientAbsent(id: string, request: RevertClientAbsentRequest, signal?: AbortSignal): Promise<BookingResponse>;

  /**
   * Lists the bookings of the signed-in photographer, earliest session first.
   * @param signal Optional signal to cancel the request.
   * @returns The booking summaries.
   */
  listBookings(signal?: AbortSignal): Promise<readonly BookingSummaryResponse[]>;

  /**
   * Reads a booking.
   * @param id Booking identifier.
   * @param signal Optional signal to cancel the request.
   * @returns The booking.
   */
  getBooking(id: string, signal?: AbortSignal): Promise<BookingResponse>;
}
