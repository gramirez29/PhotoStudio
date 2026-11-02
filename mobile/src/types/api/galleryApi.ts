import type {
  EditGalleryTermsRequest,
  FinalDetailsInfo,
  FinalDownloadResponse,
  FinalFileInfo,
  FinalUploadSlot,
  GalleryResponse,
  ProofFileInfo,
  ProofRegistration,
  ProofResponse,
  ProofUploadSlot,
  RegisterUploadsResult,
  SelectionResponse,
  ZipUploadSlot,
} from './gallery';

/** Gallery endpoints of the API that the app calls, with the token of the session. */
export interface GalleryApi {
  /**
   * Reads the gallery of a booking.
   * @param bookingId Booking identifier.
   * @param signal Optional signal to cancel the request.
   * @returns The gallery; the request fails with a 404 when the booking has none.
   */
  getByBooking(bookingId: string, signal?: AbortSignal): Promise<GalleryResponse>;

  /**
   * Reads a gallery.
   * @param galleryId Gallery identifier.
   * @param signal Optional signal to cancel the request.
   * @returns The gallery.
   */
  get(galleryId: string, signal?: AbortSignal): Promise<GalleryResponse>;

  /**
   * Reads what the client chose.
   * @param galleryId Gallery identifier.
   * @param signal Optional signal to cancel the request.
   * @returns The selection, with the details of each final.
   */
  getSelection(galleryId: string, signal?: AbortSignal): Promise<SelectionResponse>;

  /**
   * Lists the proofs, each with an address that works for a few minutes.
   * @param galleryId Gallery identifier.
   * @param signal Optional signal to cancel the request.
   * @returns The proofs, in the order they were uploaded.
   */
  listProofs(galleryId: string, signal?: AbortSignal): Promise<readonly ProofResponse[]>;

  /**
   * Changes how many photos the package includes, how many are a gift and what an extra costs.
   * @param galleryId Gallery identifier.
   * @param terms The new terms.
   * @returns The gallery.
   */
  editTerms(galleryId: string, terms: EditGalleryTermsRequest): Promise<GalleryResponse>;

  /**
   * Removes a proof, while the selection is not open.
   * @param galleryId Gallery identifier.
   * @param photoId Proof identifier.
   * @returns The gallery.
   */
  removeProof(galleryId: string, photoId: string): Promise<GalleryResponse>;

  /**
   * Opens the selection: the client can choose and is told by email.
   * @param galleryId Gallery identifier.
   * @returns The gallery, now in selection.
   */
  openSelection(galleryId: string): Promise<GalleryResponse>;

  /**
   * Reopens the selection so the client can choose again; the client is told by email.
   * @param galleryId Gallery identifier.
   * @param reason Why; required, kept for the photographer's record.
   * @returns The gallery, in selection again.
   */
  reopenSelection(galleryId: string, reason: string): Promise<GalleryResponse>;

  /**
   * Publishes the final photos of the package.
   * @param galleryId Gallery identifier.
   * @returns The gallery.
   */
  publishFinals(galleryId: string): Promise<GalleryResponse>;

  /**
   * Lets the client download the extra photos.
   * @param galleryId Gallery identifier.
   * @returns The gallery.
   */
  releaseExtras(galleryId: string): Promise<GalleryResponse>;

  /**
   * Deletes the ZIP.
   * @param galleryId Gallery identifier.
   * @returns The gallery without its ZIP.
   */
  removeZip(galleryId: string): Promise<GalleryResponse>;

  /**
   * Asks for the address to download one final. It is not counted as a download of the client.
   * @param galleryId Gallery identifier.
   * @param photoId Photo identifier.
   * @param signal Optional signal to cancel the request.
   * @returns The address.
   */
  getFinalDownload(galleryId: string, photoId: string, signal?: AbortSignal): Promise<FinalDownloadResponse>;

  /**
   * Asks for the places to upload some proofs. The bytes go straight to the storage, not through the API.
   * @param galleryId Gallery identifier.
   * @param files Name and size of each proof, 1 to 50.
   * @param signal Optional signal to cancel the request.
   * @returns One place per file, in the order of the request.
   */
  requestProofUploads(galleryId: string, files: readonly ProofFileInfo[], signal?: AbortSignal): Promise<readonly ProofUploadSlot[]>;

  /**
   * Registers the proofs that were uploaded; the API looks at what is really in the storage first.
   * @param galleryId Gallery identifier.
   * @param photos The uploads, 1 to 50.
   * @param signal Optional signal to cancel the request.
   * @returns The gallery and the uploads that were not accepted, each with its reason.
   */
  registerProofs(galleryId: string, photos: readonly ProofRegistration[], signal?: AbortSignal): Promise<RegisterUploadsResult>;

  /**
   * Asks for the places to upload some finals, each with the place of its thumbnail.
   * @param galleryId Gallery identifier.
   * @param files The chosen photo each final belongs to and its size, 1 to 50.
   * @param signal Optional signal to cancel the request.
   * @returns One place per file, in the order of the request.
   */
  requestFinalUploads(galleryId: string, files: readonly FinalFileInfo[], signal?: AbortSignal): Promise<readonly FinalUploadSlot[]>;

  /**
   * Registers the finals that were uploaded.
   * @param galleryId Gallery identifier.
   * @param photoIds The chosen photos whose finals were uploaded, 1 to 50.
   * @param details What the phone knows about each file, if anything.
   * @param signal Optional signal to cancel the request.
   * @returns The gallery and the uploads that were not kept, each with its reason.
   */
  registerFinals(galleryId: string, photoIds: readonly string[], details: readonly FinalDetailsInfo[], signal?: AbortSignal): Promise<RegisterUploadsResult>;

  /**
   * Asks for the place to upload the ZIP with the photos.
   * @param galleryId Gallery identifier.
   * @param bytes Size of the file.
   * @param signal Optional signal to cancel the request.
   * @returns The place.
   */
  requestZipUpload(galleryId: string, bytes: number, signal?: AbortSignal): Promise<ZipUploadSlot>;

  /**
   * Registers the ZIP that was uploaded; the API looks at what is really in the storage first.
   * @param galleryId Gallery identifier.
   * @param signal Optional signal to cancel the request.
   * @returns The gallery, with its ZIP.
   */
  registerZip(galleryId: string, signal?: AbortSignal): Promise<GalleryResponse>;
}
