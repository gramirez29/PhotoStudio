import type { ProofUploadSlot } from '../api/proofs';
import type { StudioGallery } from '../api/studio';
import type { StudioApi } from '../api/studioApi';
import type { ProofRenderer } from './proofRenderer.types';

/** Where one photo of the queue is. */
export type UploadItemStatus = 'waiting' | 'preparing' | 'uploading' | 'registering' | 'failed';

/** One photo of the queue of an upload. */
export interface UploadItem {
  /** Number that tells the photo apart in the queue. */
  readonly id: number;
  /** The photo the person chose. */
  readonly file: File;
  /** Where the photo is. */
  readonly status: UploadItemStatus;
  /** Why it failed, in words, when `status` is `failed`; otherwise null. */
  readonly problem: string | null;
}

/** What the uploader needs from outside; tests give others. */
export interface ProofUploaderDeps {
  /** The two requests of the API that an upload uses. */
  readonly api: Pick<StudioApi, 'requestProofUploads' | 'registerProofs'>;
  /** Turns photos into proofs. */
  readonly renderer: ProofRenderer;
  /**
   * Sends a proof to the storage.
   * @param slot The place the API gave.
   * @param proof The proof.
   * @param signal Optional signal to cancel the upload.
   * @returns A promise that resolves when the storage has the proof.
   */
  readonly put: (slot: ProofUploadSlot, proof: Blob, signal?: AbortSignal) => Promise<void>;
}

/** What the uploader tells the page while it works, so the page can show it. */
export interface UploadObserver {
  /**
   * A photo changed where it is.
   * @param id The photo of the queue.
   * @param status Where it is now.
   * @param problem Why it failed, when it did.
   */
  onItem(id: number, status: UploadItemStatus, problem: string | null): void;

  /**
   * A photo was registered: it is a proof of the gallery now and leaves the queue.
   * @param id The photo of the queue.
   */
  onDone(id: number): void;

  /**
   * A batch was registered.
   * @param gallery The gallery after it, with its new count of proofs.
   */
  onRegistered(gallery: StudioGallery): void;
}

/** The uploader of proofs. */
export interface ProofUploader {
  /**
   * Uploads a list of photos to a gallery.
   * @param galleryId Gallery the proofs go to.
   * @param items The photos to upload.
   * @param observer Told about every change, so the page can show it.
   * @param signal Optional signal to cancel the run (for example when the page is left).
   * @returns How the run ended.
   */
  run(galleryId: string, items: readonly UploadItem[], observer: UploadObserver, signal?: AbortSignal): Promise<UploadOutcome>;
}

/** How a run of the uploader ended. */
export interface UploadOutcome {
  /** True when a request failed and the rest of the photos were left waiting, to try again when the person decides. */
  readonly stopped: boolean;
  /** Why it stopped, in words; null when it did not. */
  readonly problem: string | null;
}
