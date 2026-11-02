import type { GALLERY_ACTIONS, GALLERY_STATUSES } from '../../constants/gallery';
import type { MoneyResponse } from './booking';

/** Where a gallery is in its life. */
export type GalleryStatus = (typeof GALLERY_STATUSES)[number];

/** What the photographer can do with a gallery right now; the API computes it. */
export type GalleryAction = (typeof GALLERY_ACTIONS)[number];

/** The ZIP with the photos that the photographer made and uploaded, and what the client did with it. */
export interface GalleryZip {
  /** Size in bytes. */
  readonly bytes: number;
  /** Instant it was registered (ISO 8601). */
  readonly uploadedAt: string;
  /** How many times the client asked to download it. */
  readonly downloads: number;
  /** Last instant the client asked to download it (ISO 8601), or null if they never did. */
  readonly lastDownloadedAt: string | null;
}

/** A gallery as the photographer sees it (backend `GalleryResponse`). */
export interface GalleryResponse {
  /** Gallery identifier (GUID). */
  readonly id: string;
  /** Booking it comes from (GUID). */
  readonly bookingId: string;
  /** Where it is. */
  readonly status: GalleryStatus;
  /** Name of the client. */
  readonly clientName: string;
  /** Name of the booked package. */
  readonly packageName: string;
  /** Start of the session (ISO 8601). */
  readonly sessionStart: string;
  /** Photos the package includes. */
  readonly includedPhotos: number;
  /** Photos given on top. */
  readonly bonusPhotos: number;
  /** Photos the client can pick without paying more (included plus gift). */
  readonly selectablePhotos: number;
  /** Price of each photo beyond those. */
  readonly extraPhotoPrice: MoneyResponse;
  /** Most photos a gallery holds. */
  readonly maxPhotos: number;
  /** Proofs uploaded so far. */
  readonly photoCount: number;
  /** Instant it was created (ISO 8601). */
  readonly createdAt: string;
  /** What the photographer can do with it now. */
  readonly allowedActions: readonly GalleryAction[];
  /** How many photos the client chose; 0 until they send the selection. */
  readonly selectedPhotoCount: number;
  /** How many of those go beyond what the package includes. */
  readonly extraPhotoCount: number;
  /** Instant the client sent the selection (ISO 8601), or null while they did not. */
  readonly selectionSubmittedAt: string | null;
  /** Instant the final photos of the package were published (ISO 8601), or null while they were not. */
  readonly finalsPublishedAt: string | null;
  /** Instant the gallery was delivered, with a zero balance (ISO 8601), or null while it was not. */
  readonly deliveredAt: string | null;
  /** Instant the extra photos were released to the client (ISO 8601), or null while they were not. */
  readonly extrasReleasedAt: string | null;
  /** The ZIP with the photos, or null while there is none. */
  readonly zip: GalleryZip | null;
  /** Last instant the selection was reopened (ISO 8601), or null if it never was. */
  readonly selectionReopenedAt: string | null;
  /** Why the selection was last reopened, or null if it never was. It is for the photographer's own record. */
  readonly selectionReopenReason: string | null;
}

/** A photo the client chose, with what the photographer knows about its final (backend `SelectedPhotoResponse`). */
export interface SelectedPhoto {
  /** Photo identifier (GUID). */
  readonly id: string;
  /** Name of the original file: what to look for in the editing software. */
  readonly name: string;
  /** Whether it is beyond what the package covers: the first photos chosen are the package, the rest are extras. */
  readonly isExtra: boolean;
  /** Size of its final photo in bytes, or null while the photographer has not uploaded it. */
  readonly finalBytes: number | null;
  /** Width of the final in pixels, or null when unknown. */
  readonly finalWidth: number | null;
  /** Height of the final in pixels, or null when unknown. */
  readonly finalHeight: number | null;
  /** Instant the final was taken (ISO 8601), or null when its metadata did not say. */
  readonly finalCapturedAt: string | null;
  /** How many times the client asked to download the final. */
  readonly finalDownloads: number;
  /** Signed address of a small image of the photo (valid ten minutes), or null when there is none. It is a secret: it is never logged. */
  readonly thumbUrl: string | null;
  /** Whether `thumbUrl` is the thumbnail of the final (true) or only the proof, which has the watermark (false). */
  readonly thumbIsFinal: boolean;
}

/** What the client chose (backend `SelectionResponse`). */
export interface SelectionResponse {
  /** Instant the client sent it (ISO 8601), or null while they did not. */
  readonly submittedAt: string | null;
  /** How many of the photos are extra. */
  readonly extraPhotos: number;
  /** What the extra photos cost. */
  readonly extraTotal: MoneyResponse;
  /** The photos, in the order they were chosen. */
  readonly photos: readonly SelectedPhoto[];
}

/** A proof of the gallery, with a signed address that works for a few minutes (backend `ProofItemResponse`). */
export interface ProofResponse {
  /** Photo identifier (GUID). */
  readonly id: string;
  /** Name of the original file. */
  readonly name: string;
  /** Size of the proof in bytes. */
  readonly bytes: number;
  /** Signed address to show it. It is a secret that expires: it is never logged. */
  readonly url: string;
  /** Instant the address stops working (ISO 8601). */
  readonly expiresAt: string;
}

/** An address to download one final photo (backend `FinalDownloadResponse`). */
export interface FinalDownloadResponse {
  /** Name to save the file with. */
  readonly fileName: string;
  /** Signed address; lasts a few minutes. It is a secret: it is never logged. */
  readonly url: string;
  /** Instant the address stops working (ISO 8601). */
  readonly expiresAt: string;
}

/** The new terms of a gallery (body of `POST /api/galleries/{id}/terms`). */
export interface EditGalleryTermsRequest {
  /** Photos the package includes. */
  readonly includedPhotos: number;
  /** Photos given on top. */
  readonly bonusPhotos: number;
  /** Price of each extra photo, in the currency of the booking. */
  readonly extraPhotoPrice: number;
}

/** A file about to be uploaded as a proof, as the API is told about it. */
export interface ProofFileInfo {
  /** Name of the original file, kept as a label. */
  readonly name: string;
  /** Size of the proof in bytes. */
  readonly bytes: number;
}

/** A place to upload one proof or final, given by the API. */
export interface ProofUploadSlot {
  /** Identifier the photo will have; it goes back when the upload is registered. */
  readonly photoId: string;
  /** Signed address to PUT the file to. It is a secret that expires: it is never logged or shown. */
  readonly url: string;
  /** Headers the upload must send exactly as given (the content type is part of the signature). */
  readonly headers: Readonly<Record<string, string>>;
  /** Instant the address stops working (ISO 8601). */
  readonly expiresAt: string;
}

/** A place to upload one final and, optionally, its thumbnail. */
export interface FinalUploadSlot extends ProofUploadSlot {
  /** Signed address to PUT the thumbnail to, or null when the API gives none. It is a secret that expires: it is never logged. */
  readonly thumbUrl: string | null;
  /** Headers the thumbnail upload must send exactly as given. */
  readonly thumbHeaders: Readonly<Record<string, string>>;
}

/** An upload of a proof that finished and has to be registered. */
export interface ProofRegistration {
  /** Identifier of the slot it was uploaded to. */
  readonly photoId: string;
  /** Name of the original file, kept as a label. */
  readonly name: string;
}

/** A final about to be uploaded, as the API is told about it. */
export interface FinalFileInfo {
  /** Identifier of the chosen photo the final belongs to. */
  readonly photoId: string;
  /** Size of the final in bytes. */
  readonly bytes: number;
}

/** What the phone knows about a final photo before sending it, as the API takes it. Every value may be unknown. */
export interface FinalDetailsInfo {
  /** Identifier of the chosen photo the final belongs to. */
  readonly photoId: string;
  /** Width in pixels, or null when unknown. */
  readonly width: number | null;
  /** Height in pixels, or null when unknown. */
  readonly height: number | null;
  /** Instant the photo was taken (ISO 8601), or null when unknown. */
  readonly capturedAt: string | null;
}

/** An upload the API did not register, and why. */
export interface UploadRejection {
  /** Identifier of the photo. */
  readonly photoId: string;
  /** Stable reason: `missing`, `wrong_type`, `too_big`, `empty`, `invalid_name`, `gallery_full`... (others may come later). */
  readonly reason: string;
}

/** What registering a batch of proofs or finals gave back. */
export interface RegisterUploadsResult {
  /** The gallery after registering what was valid. */
  readonly gallery: GalleryResponse;
  /** The uploads that were not registered. */
  readonly rejected: readonly UploadRejection[];
}

/** The place to upload the ZIP, as the API gives it. */
export interface ZipUploadSlot {
  /** Signed address to send the file to with a PUT. It is a secret that expires: it is never logged. */
  readonly url: string;
  /** Headers that must go with the request. */
  readonly headers: Readonly<Record<string, string>>;
  /** Instant the address stops working (ISO 8601). */
  readonly expiresAt: string;
}
