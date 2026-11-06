/** What in the bookings of the photographer is waiting for them (backend `BookingAttentionResponse`). */
export interface BookingAttentionResponse {
  /** Bookings with a proof of payment that has not been verified or rejected yet. */
  readonly pendingProofs: number;
  /** Tentative bookings whose hold ends within 24 hours. */
  readonly expiringSoon: number;
}
