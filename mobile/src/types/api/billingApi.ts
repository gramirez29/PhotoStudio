import type { CompleteRefundRequest, RefundListResponse, SettlementResponse } from './billing';

/** Billing endpoints used by the photographer app. */
export interface BillingApi {
  /**
   * Lists the refunds the photographer still has to give back.
   * @param signal Optional signal to cancel the request.
   * @returns The pending refunds and how many there are.
   */
  listRefunds(signal?: AbortSignal): Promise<RefundListResponse>;

  /**
   * Reads the settlement of a booking.
   * @param bookingId Booking identifier.
   * @param signal Optional signal to cancel the request.
   * @returns The settlement; the request fails with 404 when the booking has none.
   */
  getSettlement(bookingId: string, signal?: AbortSignal): Promise<SettlementResponse>;

  /**
   * Records that the refund of a booking was given back to the client.
   * @param bookingId Booking identifier.
   * @param request Method and optional note.
   * @param signal Optional signal to cancel the request.
   * @returns The settlement after the refund was recorded.
   */
  completeRefund(bookingId: string, request: CompleteRefundRequest, signal?: AbortSignal): Promise<SettlementResponse>;
}
