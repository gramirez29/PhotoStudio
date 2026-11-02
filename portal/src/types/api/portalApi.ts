import type { PortalBooking, PortalCodeSent, PortalContract, PortalGallery, PortalGallerySummary, PortalSession, SignContractRequest, SubmitProofRequest } from './portal';

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

  /**
   * Reads the contract of the booking, with its data filled in. It needs the session because the text holds personal data.
   * @param token Secret token taken from the URL.
   * @param accessToken Token of the session obtained by verifying a code.
   * @param signal Optional signal to cancel the request.
   * @returns The contract text and whether it is signed.
   */
  getContract(token: string, accessToken: string, signal?: AbortSignal): Promise<PortalContract>;

  /**
   * Signs the contract on behalf of the client.
   * @param token Secret token taken from the URL.
   * @param accessToken Token of the session obtained by verifying a code.
   * @param request What the client declared, the version they read and their acceptance.
   * @param signal Optional signal to cancel the request.
   * @returns The booking after signing.
   */
  signContract(token: string, accessToken: string, request: SignContractRequest, signal?: AbortSignal): Promise<PortalBooking>;

  /**
   * Asks where the gallery of the booking is. It needs only the link.
   * @param token Secret token taken from the URL.
   * @param signal Optional signal to cancel the request.
   * @returns The summary; the request fails with a 404 when there is no gallery for the client to see yet.
   */
  getGallerySummary(token: string, signal?: AbortSignal): Promise<PortalGallerySummary>;

  /**
   * Reads the gallery: the proofs and the terms to choose them. It needs the session, because the photos are not paid yet.
   * @param token Secret token taken from the URL.
   * @param accessToken Token of the session obtained by verifying a code.
   * @param signal Optional signal to cancel the request.
   * @returns The gallery.
   */
  getGallery(token: string, accessToken: string, signal?: AbortSignal): Promise<PortalGallery>;

  /**
   * Sends the photos the client chose. From then on the selection is locked.
   * @param token Secret token taken from the URL.
   * @param accessToken Token of the session obtained by verifying a code.
   * @param photoIds Identifiers of the photos chosen.
   * @param signal Optional signal to cancel the request.
   * @returns The gallery after the selection.
   */
  submitSelection(token: string, accessToken: string, photoIds: readonly string[], signal?: AbortSignal): Promise<PortalGallery>;
}
