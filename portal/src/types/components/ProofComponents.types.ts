import type { StudioGallery, StudioUser } from '../api/studio';
import type { UploadItem } from '../media/proofUploader.types';
import type { FinalMatch } from '../utils/finalMatching.types';

/** Props of the `StudioGalleryDetail` component. */
export interface StudioGalleryDetailProps {
  /** Identifier of the gallery in the address. */
  readonly galleryId: string;
  /** The photographer who is signed in. */
  readonly user: StudioUser;
}

/** Props of the `StudioProofs` component. */
export interface StudioProofsProps {
  /** The gallery whose proofs are shown. */
  readonly gallery: StudioGallery;
}

/** Props of the `StudioSelection` component. */
export interface StudioSelectionProps {
  /** The gallery whose client sent the selection. */
  readonly gallery: StudioGallery;
}

/** Props of the `StudioFinals` component. */
export interface StudioFinalsProps {
  /** The gallery whose client sent the selection. */
  readonly gallery: StudioGallery;
}

/** Props of the `FinalsPicker` component. */
export interface FinalsPickerProps {
  /** True while an upload is running: nothing more can be chosen. */
  readonly disabled: boolean;
  /** Called with the files the person chose. */
  readonly onChoose: (files: readonly File[]) => void;
}

/** Props of the `FinalsStaged` component. */
export interface FinalsStagedProps {
  /** The files paired with the photo of the selection each is the final of. */
  readonly staged: readonly FinalMatch[];
  /** Called when the person confirms. */
  readonly onConfirm: () => void;
  /** Called when the person changes their mind. */
  readonly onCancel: () => void;
}

/** Props of the `PublishFinalsPanel` component. */
export interface PublishFinalsPanelProps {
  /** The gallery whose finals are published. */
  readonly gallery: StudioGallery;
  /** True while finals are still going up: publishing then would leave some out. */
  readonly disabled: boolean;
}

/** Props of the `ReleaseExtrasPanel` component. */
export interface ReleaseExtrasPanelProps {
  /** The gallery whose extras are released. */
  readonly gallery: StudioGallery;
  /** True while finals are still going up. */
  readonly disabled: boolean;
}

/** Props of the `ProofPicker` component. */
export interface ProofPickerProps {
  /** How many more proofs the gallery can hold. */
  readonly room: number;
  /** True while an upload is running: nothing more can be chosen. */
  readonly disabled: boolean;
  /** Called with the files the person chose. */
  readonly onChoose: (files: readonly File[]) => void;
}

/** Props of the `ProofSamplePreview` component. */
export interface ProofSamplePreviewProps {
  /** The files waiting for the person to say go; the first one is used to make the sample. */
  readonly files: readonly File[];
  /** Called when the person confirms. */
  readonly onConfirm: () => void;
  /** Called when the person changes their mind. */
  readonly onCancel: () => void;
}

/** Props of the `ProofQueue` component. */
export interface ProofQueueProps {
  /** The photos not registered yet. */
  readonly items: readonly UploadItem[];
  /** True while photos are being prepared, sent and registered. */
  readonly running: boolean;
  /** Why the run stopped, when it did. */
  readonly problem: string | null;
  /** Called to try again with the photos that failed or were left waiting. */
  readonly onRetry: () => void;
  /** Called to take the failed photos out of the list. */
  readonly onDismiss: () => void;
}

/** Props of the `ProofGrid` component. */
export interface ProofGridProps {
  /** The gallery whose proofs are shown. */
  readonly gallery: StudioGallery;
  /** Whether the photographer can still remove proofs. */
  readonly canRemove: boolean;
}

/** Props of the `OpenSelectionPanel` component. */
export interface OpenSelectionPanelProps {
  /** The gallery to open. */
  readonly gallery: StudioGallery;
  /** True while photos are still going up: publishing then would leave some out. */
  readonly disabled: boolean;
}
