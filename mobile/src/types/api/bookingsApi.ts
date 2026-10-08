import type {
  BookingResponse,
  BookingSummaryResponse,
  CreateBookingRequest,
  RescheduleBookingRequest,
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
   * Lists the photographer's bookings, earliest session first.
   * @param photographerId Photographer (tenant) identifier.
   * @param signal Optional signal to cancel the request.
   * @returns The booking summaries.
   */
  listBookings(photographerId: string, signal?: AbortSignal): Promise<readonly BookingSummaryResponse[]>;

  /**
   * Reads a booking.
   * @param id Booking identifier.
   * @param signal Optional signal to cancel the request.
   * @returns The booking.
   */
  getBooking(id: string, signal?: AbortSignal): Promise<BookingResponse>;
}
