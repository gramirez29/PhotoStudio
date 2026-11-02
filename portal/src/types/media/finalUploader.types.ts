import type { ProofUploadSlot } from '../api/proofs';
import type { StudioApi } from '../api/studioApi';
import type { UploadItem, UploadObserver, UploadOutcome } from './proofUploader.types';

/** One final of the queue: the file and the photo of the selection it belongs to. */
export interface FinalUploadItem extends UploadItem {
  /** Identifier of the chosen photo the final belongs to. */
  readonly photoId: string;
}

/** What the uploader of finals needs from outside; tests give others. */
export interface FinalUploaderDeps {
  /** The two requests of the API that an upload of finals uses. */
  readonly api: Pick<StudioApi, 'requestFinalUploads' | 'registerFinals'>;
  /**
   * Sends a final to the storage.
   * @param slot The place the API gave.
   * @param file The final.
   * @param signal Optional signal to cancel the upload.
   * @returns A promise that resolves when the storage has the file.
   */
  readonly put: (slot: ProofUploadSlot, file: Blob, signal?: AbortSignal) => Promise<void>;
}

/** The uploader of final photos. */
export interface FinalUploader {
  /**
   * Uploads a list of finals to a gallery, a few at a time: the places are asked right before each batch, the files go straight to
   * the storage and the batch is registered before the next starts. A file that fails does not stop the others; a request that fails
   * (the API, or no connection) stops the run and leaves the rest waiting.
   * @param galleryId Gallery the finals belong to.
   * @param items The finals to upload.
   * @param observer Told about every change, so the page can show it.
   * @param signal Optional signal to cancel the run.
   * @returns How the run ended.
   */
  run(galleryId: string, items: readonly FinalUploadItem[], observer: UploadObserver, signal?: AbortSignal): Promise<UploadOutcome>;
}
