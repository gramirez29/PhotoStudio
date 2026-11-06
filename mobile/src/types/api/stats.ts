import type { BookingStatus } from './booking';

/** How many bookings there are in one status (backend `StatusCountResponse`). */
export interface StatusCountResponse {
  /** The status. */
  readonly status: BookingStatus;
  /** Number of bookings in it; zero when there are none. */
  readonly count: number;
}

/** The bookings of the photographer counted by status (backend `BookingStatsResponse`). */
export interface BookingStatsResponse {
  /** All the bookings counted. */
  readonly total: number;
  /** One entry for every status, in the order of the backend, with zero for the ones without bookings. */
  readonly byStatus: readonly StatusCountResponse[];
}
