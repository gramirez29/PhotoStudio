import type { PortalBooking, PortalCodeSent, PortalSession, SubmitProofRequest } from './portal';

/** Portal endpoints the page calls. */
export interface PortalApi {
  /**
   * Reads the booking a link opens.
   * @param token Secret token taken from the URL.
   * @param signal Optional signal to cancel the request.
   * @returns The booking as the client sees it.
   */
  getBooking(token: string, signal?: AbortSignal): Promise<PortalBooking>;

  /**
   * Asks for a verification code, which the server emails to the address the photographer registered.
   * @param token Secret token taken from the URL.
   * @param signal Optional signal to cancel the request.
   * @returns Where the code went (masked) and when it expires.
   */
  requestCode(token: string, signal?: AbortSignal): Promise<PortalCodeSent>;

  /**
   * Exchanges the code the client received for a session that opens this booking.
   * @param token Secret token taken from the URL.
   * @param code The six digits the client typed.
   * @param signal Optional signal to cancel the request.
   * @returns The session.
   */
  verifyCode(token: string, code: string, signal?: AbortSignal): Promise<PortalSession>;

  /**
   * Reports a SINPE Móvil transfer for the photographer to verify.
   * @param token Secret token taken from the URL.
   * @param accessToken Token of the session obtained by verifying a code.
   * @param request Amount, reference and idempotency key.
   * @param signal Optional signal to cancel the request.
   * @returns The booking after the proof.
   */
  submitProof(token: string, accessToken: string, request: SubmitProofRequest, signal?: AbortSignal): Promise<PortalBooking>;
}
