import type { RescheduleBookingRequest } from '../api/booking';

/** Current slot of a booking, as the API sends it. */
export interface SessionSlotIso {
  /** Session start (ISO 8601). */
  readonly sessionStart: string;
  /** Session end (ISO 8601). */
  readonly sessionEnd: string;
}

/** Result of validating the reschedule form: the request to send, or the message to show. */
export type RescheduleFormResult =
  | { readonly ok: true; readonly request: RescheduleBookingRequest }
  | { readonly ok: false; readonly error: string };
