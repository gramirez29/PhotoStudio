import type { FinalDetailsInfo, FinalDownloadLink, FinalFileInfo, FinalUploadSlot, ZipUploadSlot } from './finals';
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
   * Lists the galleries whose space can be freed: the delivered ones, the one delivered longest ago first.
   * @param signal Optional signal to cancel the request.
   * @returns The galleries.
   */
  listArchivable(signal?: AbortSignal): Promise<readonly StudioGallery[]>;

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

  /**
   * Reopens the selection of the client so they can choose again. The client is told by email. What they chose is discarded; the
   * finals already uploaded stay tied to their photo.
   * @param galleryId Gallery identifier.
   * @param reason Why it is reopened; required, kept for the photographer's record.
   * @returns The gallery, in selection again.
   */
  reopenSelection(galleryId: string, reason: string): Promise<StudioGallery>;

  /**
   * Asks for the places to upload final photos, one per photo of the selection. The bytes go straight to the storage.
   * @param galleryId Gallery identifier.
   * @param files The finals about to be uploaded, 1 to 50.
   * @param signal Optional signal to cancel the request.
   * @returns One slot per file, in the same order.
   */
  requestFinalUploads(galleryId: string, files: readonly FinalFileInfo[], signal?: AbortSignal): Promise<readonly FinalUploadSlot[]>;

  /**
   * Registers the finals that were uploaded; the API looks at what is really in the storage first.
   * @param galleryId Gallery identifier.
   * @param photoIds The photos of the selection whose finals were uploaded, 1 to 50.
   * @param details What the browser read from each file (size in pixels, date taken); may be empty.
   * @param signal Optional signal to cancel the request.
   * @returns The gallery and the finals that were not accepted.
   */
  registerFinals(galleryId: string, photoIds: readonly string[], details: readonly FinalDetailsInfo[], signal?: AbortSignal): Promise<RegisterProofsResult>;

  /**
   * Asks for the place to upload the ZIP with the photos, which the photographer made by hand. The bytes go straight to the storage.
   * @param galleryId Gallery identifier.
   * @param bytes Size of the file.
   * @param signal Optional signal to cancel the request.
   * @returns The place to upload to.
   */
  requestZipUpload(galleryId: string, bytes: number, signal?: AbortSignal): Promise<ZipUploadSlot>;

  /**
   * Registers the ZIP that was uploaded; the API looks at what is really in the storage first.
   * @param galleryId Gallery identifier.
   * @param signal Optional signal to cancel the request.
   * @returns The gallery with its ZIP.
   */
  registerZip(galleryId: string, signal?: AbortSignal): Promise<StudioGallery>;

  /**
   * Deletes the ZIP, from the gallery and from the storage.
   * @param galleryId Gallery identifier.
   * @returns The gallery without its ZIP.
   */
  removeZip(galleryId: string): Promise<StudioGallery>;

  /**
   * Frees the space of a delivered gallery: every photo and the ZIP are deleted from the storage and the gallery is archived. It cannot be undone.
   * @param galleryId Gallery identifier.
   * @returns The gallery, archived.
   */
  releaseGallery(galleryId: string): Promise<StudioGallery>;

  /**
   * Asks for the address to download one final photo. It is not counted as a download of the client.
   * @param galleryId Gallery identifier.
   * @param photoId Identifier of the chosen photo.
   * @param signal Optional signal to cancel the request.
   * @returns The address and the name to save the file with.
   */
  getFinalDownload(galleryId: string, photoId: string, signal?: AbortSignal): Promise<FinalDownloadLink>;

  /**
   * Publishes the final photos of the package; the client downloads them as soon as the balance is zero.
   * @param galleryId Gallery identifier.
   * @returns The gallery, now with its finals published.
   */
  publishFinals(galleryId: string): Promise<StudioGallery>;

  /**
   * Lets the client download the extra photos, once the photographer agreed on their payment with the client.
   * @param galleryId Gallery identifier.
   * @returns The gallery, with the extras released.
   */
  releaseExtras(galleryId: string): Promise<StudioGallery>;
}
