/** What a maintenance pass did, as the backend reports it (`POST /api/maintenance/run`). */
export interface MaintenanceResponse {
  /** Tentative bookings that expired and released their slot. */
  readonly bookingsExpired: number;
  /** Bookings that could not be expired right now and will be tried again on the next pass. */
  readonly bookingsSkipped: number;
  /** Outbox messages that were claimed for delivery. */
  readonly eventsProcessed: number;
  /** Whether the pass stopped at its size limit and a new pass would find more to do. */
  readonly moreWorkPending: boolean;
}
