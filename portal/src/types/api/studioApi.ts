import type { ProofFileInfo, ProofRegistration, ProofUploadSlot, RegisterProofsResult, StudioProof } from './proofs';
import type { StudioCodeSent, StudioGallery, StudioGalleryPage, StudioLoginResult, StudioSelection, StudioUser } from './studio';

/** Endpoints of the web area of the photographer. */
export interface StudioApi {
  /**
   * Asks who is signed in.
   * @param signal Optional signal to cancel the request.
   * @returns The user; the request fails with a 401 when nobody is.
   */
  getSession(signal?: AbortSignal): Promise<StudioUser>;

  /**
   * Gives the password. A remembered browser gets its session; any other gets a code in the email.
   * @param username Username as typed.
   * @param password Password as typed.
   * @returns The session, or where the code went.
   */
  login(username: string, password: string): Promise<StudioLoginResult>;

  /**
   * Gives the code from the email and starts the session.
   * @param code The six digits.
   * @param rememberDevice Whether to skip the code in this browser for 30 days.
   * @returns The user who signed in.
   */
  verifyCode(code: string, rememberDevice: boolean): Promise<StudioUser>;

  /**
   * Asks for another code.
   * @returns Where the code went.
   */
  resendCode(): Promise<StudioCodeSent>;

  /**
   * Signs out, closing the session on the server.
   * @param forgetDevice Whether to also forget this browser, so the next sign-in asks for the code again.
   * @returns A promise that resolves when the session is closed.
   */
  logout(forgetDevice: boolean): Promise<void>;

  /**
   * Loads one page of the galleries of the photographer.
   * @param cursor The `nextCursor` of the previous page, or null for the first page.
   * @param signal Optional signal to cancel the request.
   * @returns The page.
   */
  listGalleries(cursor: string | null, signal?: AbortSignal): Promise<StudioGalleryPage>;

  /**
   * Loads one gallery of the photographer.
   * @param galleryId Gallery identifier.
   * @param signal Optional signal to cancel the request.
   * @returns The gallery; the request fails with a 404 when it does not exist or is another photographer's.
   */
  getGallery(galleryId: string, signal?: AbortSignal): Promise<StudioGallery>;

  /**
   * Loads what the client chose, with the names of the original files.
   * @param galleryId Gallery identifier.
   * @param signal Optional signal to cancel the request.
   * @returns The selection; empty while the client has not sent it.
   */
  getSelection(galleryId: string, signal?: AbortSignal): Promise<StudioSelection>;

  /**
   * Loads the proofs of a gallery, each with a signed address that works for a few minutes.
   * @param galleryId Gallery identifier.
   * @param signal Optional signal to cancel the request.
   * @returns The proofs, in the order they were registered.
   */
  listProofs(galleryId: string, signal?: AbortSignal): Promise<readonly StudioProof[]>;

  /**
   * Asks for the places to upload proofs, one per file. The bytes go straight to the storage, not through the API.
   * @param galleryId Gallery identifier.
   * @param files The proofs about to be uploaded, 1 to 50.
   * @param signal Optional signal to cancel the request.
   * @returns One slot per file, in the same order.
   */
  requestProofUploads(galleryId: string, files: readonly ProofFileInfo[], signal?: AbortSignal): Promise<readonly ProofUploadSlot[]>;

  /**
   * Registers the proofs that were uploaded; the API looks at what is really in the storage first.
   * @param galleryId Gallery identifier.
   * @param photos The uploads, 1 to 50.
   * @param signal Optional signal to cancel the request.
   * @returns The gallery and the proofs that were not accepted.
   */
  registerProofs(galleryId: string, photos: readonly ProofRegistration[], signal?: AbortSignal): Promise<RegisterProofsResult>;

  /**
   * Removes a proof, while the selection is not open.
   * @param galleryId Gallery identifier.
   * @param photoId Identifier of the proof.
   * @returns The gallery after removing it.
   */
  removeProof(galleryId: string, photoId: string): Promise<StudioGallery>;

  /**
   * Opens the selection: the proofs are published and the client is told by email.
   * @param galleryId Gallery identifier.
   * @returns The gallery, now in selection.
   */
  openSelection(galleryId: string): Promise<StudioGallery>;
}
