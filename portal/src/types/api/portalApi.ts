import type { PortalBooking } from './portal';

/** Portal endpoints the page calls. */
export interface PortalApi {
  /**
   * Reads the booking a link opens.
   * @param token Secret token taken from the URL.
   * @param signal Optional signal to cancel the request.
   * @returns The booking as the client sees it.
   */
  getBooking(token: string, signal?: AbortSignal): Promise<PortalBooking>;
}
