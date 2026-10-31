import type { GALLERY_ACTIONS, GALLERY_STATUSES } from '../../constants/gallery';
import type { Money } from './portal';

/** State of a gallery. */
export type GalleryStatus = (typeof GALLERY_STATUSES)[number];

/** Something the photographer can do with a gallery. */
export type GalleryAction = (typeof GALLERY_ACTIONS)[number];

/** The photographer who is signed in, as the web area shows them: the email is masked. */
export interface StudioUser {
  /** Identifier of the photographer. */
  readonly photographerId: string;
  /** Username. */
  readonly username: string;
  /** Name, for the greeting. */
  readonly name: string;
  /** Email with the middle hidden; empty where the API does not send it. */
  readonly maskedEmail: string;
}

/** The sign-in is complete: the session cookie is set. */
export interface StudioSignedIn {
  /** Always `SignedIn`. */
  readonly status: 'SignedIn';
  /** Who signed in. */
  readonly user: StudioUser;
}

/** A code went to the email and has to come back to finish the sign-in. */
export interface StudioCodeRequired {
  /** Always `CodeRequired`. */
  readonly status: 'CodeRequired';
  /** Where the code went, with the middle hidden. */
  readonly maskedEmail: string;
  /** Instant the code stops being accepted, ISO 8601. */
  readonly expiresAt: string;
}

/** How the password step of the sign-in ended. */
export type StudioLoginResult = StudioSignedIn | StudioCodeRequired;

/** Confirmation that a new code was sent. */
export interface StudioCodeSent {
  /** Where the code went, with the middle hidden. */
  readonly maskedEmail: string;
  /** Instant the code stops being accepted, ISO 8601. */
  readonly expiresAt: string;
}

/** A gallery of the photographer, as the web area lists it. */
export interface StudioGallery {
  /** Gallery identifier. */
  readonly id: string;
  /** Booking it comes from. */
  readonly bookingId: string;
  /** Where it is in its life. */
  readonly status: GalleryStatus;
  /** Name of the client. */
  readonly clientName: string;
  /** Name of the booked package. */
  readonly packageName: string;
  /** Start of the session, ISO 8601. */
  readonly sessionStart: string;
  /** Photos the package includes. */
  readonly includedPhotos: number;
  /** Photos given on top. */
  readonly bonusPhotos: number;
  /** Photos the client can pick without paying more. */
  readonly selectablePhotos: number;
  /** Price of each photo beyond those. */
  readonly extraPhotoPrice: Money;
  /** Most photos a gallery holds. */
  readonly maxPhotos: number;
  /** Proofs registered so far. */
  readonly photoCount: number;
  /** How many photos the client chose; 0 until they send the selection. */
  readonly selectedPhotoCount: number;
  /** How many of the chosen photos go beyond what the package includes. */
  readonly extraPhotoCount: number;
  /** Instant the client sent the selection, ISO 8601, or null while they did not. */
  readonly selectionSubmittedAt: string | null;
  /** Instant it was created, ISO 8601. */
  readonly createdAt: string;
  /** What the photographer can do with it now. */
  readonly allowedActions: readonly GalleryAction[];
}

/** One page of galleries, the most recently created first. */
export interface StudioGalleryPage {
  /** The galleries of this page. */
  readonly items: readonly StudioGallery[];
  /** What to send back to get the next page, or null when this is the last one. */
  readonly nextCursor: string | null;
}

/** A photo the client chose, with the name of the file it came from: what to look for in the editing software. */
export interface StudioSelectedPhoto {
  /** Photo identifier. */
  readonly id: string;
  /** Name of the original file. */
  readonly name: string;
}

/** What the client chose in a gallery. */
export interface StudioSelection {
  /** Instant the client sent it, ISO 8601, or null while they did not. */
  readonly submittedAt: string | null;
  /** How many of the photos go beyond what the package includes. */
  readonly extraPhotos: number;
  /** What those extra photos cost. */
  readonly extraTotal: Money;
  /** The photos chosen, in the order they were chosen. */
  readonly photos: readonly StudioSelectedPhoto[];
}
