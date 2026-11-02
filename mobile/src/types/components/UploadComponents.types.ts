import type { GalleryResponse, SelectedPhoto } from '../api/gallery';
import type { UploadItem, UploadItemStatus } from '../media/upload.types';
import type { UploadQueue } from '../hooks/useUpload.types';

/** Props of the `UploadProgressBar` component. */
export interface UploadProgressBarProps {
  /** How much was done, from 0 to 1. */
  readonly fraction: number;
  /** What a screen reader says about the bar. */
  readonly accessibilityLabel: string;
}

/** Props of the `UploadQueueList` component. */
export interface UploadQueueListProps {
  /** The queue of the upload. */
  readonly queue: UploadQueue<UploadItem>;
}

/** Props of the `UploadQueueRow` component. */
export interface UploadQueueRowProps {
  /** Name of the file. */
  readonly name: string;
  /** Where the photo is. */
  readonly status: UploadItemStatus;
  /** Why it failed, when it did; otherwise null. */
  readonly problem: string | null;
  /** How much of the photo has been sent, from 0 to 1. */
  readonly progress: number;
}

/** Props of the `GalleryProofUpload` component. */
export interface GalleryProofUploadProps {
  /** The gallery the proofs go to. */
  readonly gallery: GalleryResponse;
}

/** Props of the `GalleryFinalUpload` component. */
export interface GalleryFinalUploadProps {
  /** The gallery the finals belong to. */
  readonly gallery: GalleryResponse;
  /** The photos the client chose, whose names the files are paired with. */
  readonly photos: readonly SelectedPhoto[];
}

/** Props of the `GalleryZipUpload` component. */
export interface GalleryZipUploadProps {
  /** The gallery the ZIP belongs to. */
  readonly gallery: GalleryResponse;
}
