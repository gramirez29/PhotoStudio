import type { PortalGallery, PortalProof, PortalSession } from '../api/portal';

/** Props of the `GalleryFlow` component. */
export interface GalleryFlowProps {
  /** Secret token of the booking link. */
  readonly token: string;
  /** Session the page already has (the client verified a code for another action), or null. */
  readonly session: PortalSession | null;
  /** Called when a code is verified or the session stops working, so the other flows of the page share it. */
  readonly onSessionChange: (session: PortalSession | null) => void;
}

/** Props of the `GalleryChooser` component. */
export interface GalleryChooserProps {
  /** Secret token of the booking link. */
  readonly token: string;
  /** Session obtained by verifying a code. */
  readonly session: PortalSession;
  /** Called when the server refuses the session, so the client confirms a code again. */
  readonly onSessionExpired: () => void;
  /** Called when the client closes the gallery. */
  readonly onClose: () => void;
}

/** Props of the `GalleryPicker` component. */
export interface GalleryPickerProps {
  /** Secret token of the booking link. */
  readonly token: string;
  /** Session obtained by verifying a code. */
  readonly session: PortalSession;
  /** The gallery, with its selection open. */
  readonly gallery: PortalGallery;
  /** Called when the server refuses the session. */
  readonly onSessionExpired: () => void;
  /** Called when the selection was sent, so the gallery words the confirmation as news. */
  readonly onSent: () => void;
  /** Called when the client closes the gallery. */
  readonly onClose: () => void;
}

/** Props of the `GallerySentSummary` component. */
export interface GallerySentSummaryProps {
  /** The gallery, with the selection that was sent (or, for a state after it, where it is). */
  readonly gallery: PortalGallery;
  /** True right after sending, to word it as news and not as a reminder. */
  readonly justSent: boolean;
  /** Called when the client closes the gallery. */
  readonly onClose: () => void;
}

/** Props of the `GalleryPhotoTile` component. */
export interface GalleryPhotoTileProps {
  /** The proof to show. */
  readonly proof: PortalProof;
  /** Position of the proof in the gallery, starting at 1, so the client can refer to it. */
  readonly number: number;
  /** Whether the client picked it. */
  readonly chosen: boolean;
  /** True when the selection is closed: the tile only shows. */
  readonly readOnly: boolean;
  /** Called when the client taps the photo to pick or drop it. */
  readonly onToggle: (photoId: string) => void;
  /** Called when the client asks to see the photo larger. */
  readonly onEnlarge: (photoId: string) => void;
}

/** Props of the `GalleryLightbox` component. */
export interface GalleryLightboxProps {
  /** Every proof of the gallery, in order. */
  readonly proofs: readonly PortalProof[];
  /** Position of the photo that is open. */
  readonly index: number;
  /** Identifiers of the photos picked. */
  readonly chosen: readonly string[];
  /** True when the selection is closed: the photo only shows. */
  readonly readOnly: boolean;
  /** Called when the client picks or drops the open photo. */
  readonly onToggle: (photoId: string) => void;
  /** Called with the position to go to. */
  readonly onMove: (index: number) => void;
  /** Called when the client closes the photo. */
  readonly onClose: () => void;
}
