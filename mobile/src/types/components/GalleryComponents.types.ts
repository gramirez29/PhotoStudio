import type { GalleryResponse, ProofResponse, SelectedPhoto, SelectionResponse } from '../api/gallery';
import type { BookingResponse } from '../api/booking';
import type { GalleryActionHandlers } from '../hooks/useGalleryActions.types';

/** Props of the `GalleryCard` component. */
export interface GalleryCardProps {
  /** The booking whose gallery is shown, if it has one. */
  readonly booking: BookingResponse;
}

/** Props of the `GalleryDetails` component. */
export interface GalleryDetailsProps {
  /** The gallery. */
  readonly gallery: GalleryResponse;
}

/** Props of the `GalleryActions` component. */
export interface GalleryActionsProps {
  /** The gallery. */
  readonly gallery: GalleryResponse;
  /** What each action runs. */
  readonly handlers: GalleryActionHandlers;
}

/** Props of the `GalleryProofs` component. */
export interface GalleryProofsProps {
  /** The gallery whose proofs are shown. */
  readonly gallery: GalleryResponse;
  /** What each action runs; removing a proof is one of them. */
  readonly handlers: GalleryActionHandlers;
}

/** Props of the `GalleryProofGrid` component. */
export interface GalleryProofGridProps {
  /** The proofs. */
  readonly proofs: readonly ProofResponse[];
  /** Called with a proof when the photographer long-presses it, if removing is allowed; otherwise absent. */
  readonly onRemove?: ((photoId: string) => void) | undefined;
}

/** Props of the `GalleryStatusPill` component. */
export interface GalleryStatusPillProps {
  /** The gallery whose state is shown. */
  readonly gallery: GalleryResponse;
}

/** Props of the `GallerySelection` component. */
export interface GallerySelectionProps {
  /** The gallery whose selection is shown. */
  readonly gallery: GalleryResponse;
}

/** Props of the `GallerySelectionBlock` component. */
export interface GallerySelectionBlockProps {
  /** Heading of the block. */
  readonly title: string;
  /** What the block means, in words. */
  readonly hint: string;
  /** The photos of the block. */
  readonly photos: readonly SelectedPhoto[];
  /** Identifier of the gallery, to ask for the download of a final. */
  readonly galleryId: string;
}

/** Props of the `GalleryPhotoTile` component. */
export interface GalleryPhotoTileProps {
  /** The photo. */
  readonly photo: SelectedPhoto;
  /** Called when the photographer taps the photo to see it large. */
  readonly onOpen: () => void;
  /** Called when the photographer asks to download the final of the photo. */
  readonly onDownload: () => void;
  /** True while a download is being prepared. */
  readonly downloading: boolean;
}

/** Props of the `GalleryPhotoViewer` component. */
export interface GalleryPhotoViewerProps {
  /** Identifier of the gallery, to ask for the real photo. */
  readonly galleryId: string;
  /** The photos the viewer moves between. */
  readonly photos: readonly SelectedPhoto[];
  /** Position of the photo that is shown. */
  readonly index: number;
  /** Called with the position of the photo the person moves to. */
  readonly onMove: (index: number) => void;
  /** Called when the person closes the viewer. */
  readonly onClose: () => void;
}

/** Props of the `GalleryZipCard` component. */
export interface GalleryZipCardProps {
  /** The gallery whose ZIP is shown. */
  readonly gallery: GalleryResponse;
  /** What each action runs; deleting the ZIP is one of them. */
  readonly handlers: GalleryActionHandlers;
}

/** Props of the `GalleryTermsForm` component. */
export interface GalleryTermsFormProps {
  /** The gallery whose terms are changed. */
  readonly gallery: GalleryResponse;
}

/** Props of the `GalleryReopenForm` component. */
export interface GalleryReopenFormProps {
  /** The gallery whose selection is reopened. */
  readonly gallery: GalleryResponse;
}

/** The selection of a gallery, split in the photos of the package and the extra ones. */
export interface SplitSelection {
  /** The photos the package covers. */
  readonly packagePhotos: readonly SelectedPhoto[];
  /** The photos beyond the package. */
  readonly extraPhotos: readonly SelectedPhoto[];
  /** The selection as the API sent it. */
  readonly selection: SelectionResponse;
}
