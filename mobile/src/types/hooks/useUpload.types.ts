import type { FinalUploadItem, UploadItem, UploadObserver, UploadOutcome } from '../media/upload.types';
import type { FinalMatch, PickedFile } from '../utils/upload.types';

/** Runs an uploader over a list of photos; the shape every uploader of photos has. */
export type UploadRunner<T extends UploadItem> = (galleryId: string, items: readonly T[], observer: UploadObserver, signal?: AbortSignal) => Promise<UploadOutcome>;

/** The queue of an upload and what can be done with it. */
export interface UploadQueue<T extends UploadItem> {
  /** The photos that are in the queue: waiting, in progress or failed. A photo that was registered leaves it. */
  readonly items: readonly T[];
  /** True while an uploader is running. */
  readonly running: boolean;
  /** Why the last run stopped, in words; null when it did not. */
  readonly problem: string | null;
  /**
   * Gives the number a new photo of the queue will have.
   * @returns A number nobody else has.
   */
  newId(): number;
  /**
   * Adds photos to the queue and uploads every photo that is not registered yet.
   * @param created The new photos.
   */
  start(created: readonly T[]): void;
  /** Tries again with every photo that is still in the queue. */
  retry(): void;
  /** Takes the photos that failed out of the queue. */
  dismissFailed(): void;
}

/** What the proofs section needs to upload proofs from the phone. */
export interface ProofUpload {
  /** The queue of the upload. */
  readonly queue: UploadQueue<UploadItem>;
  /** The photos chosen that wait for the photographer to confirm, after seeing the sample. */
  readonly pending: readonly PickedFile[];
  /** Address of the sample proof made from the first of them, or null while there is none. */
  readonly sample: string | null;
  /** Why the photos left out were left out, or why the sample could not be made, in words; null when there is nothing to say. */
  readonly notice: string | null;
  /** True while the picker is open or the sample is being made. */
  readonly preparing: boolean;
  /** Opens the picker, and then prepares the sample for the photographer to confirm. */
  choose(): Promise<void>;
  /** Uploads the photos that were waiting for the confirmation. */
  confirm(): void;
  /** Drops the photos that were waiting for the confirmation. */
  cancel(): void;
}

/** What the section of the finals needs to upload finals from the phone. */
export interface FinalUpload {
  /** The queue of the upload. */
  readonly queue: UploadQueue<FinalUploadItem>;
  /** The files chosen that matched a photo of the selection and wait for the confirmation. */
  readonly pending: readonly FinalMatch[];
  /** Which files were left out and why, in words; null when none was. */
  readonly notice: string | null;
  /** True while the picker is open. */
  readonly preparing: boolean;
  /** Opens the picker and pairs what was chosen with the photos of the selection. */
  choose(): Promise<void>;
  /** Uploads the files that were waiting for the confirmation. */
  confirm(): void;
  /** Drops the files that were waiting for the confirmation. */
  cancel(): void;
}

/** What the card of the ZIP needs to upload it from the phone. */
export interface ZipUpload {
  /** The ZIP chosen that waits for the confirmation or is being sent, or null. */
  readonly file: PickedFile | null;
  /** True while the ZIP is being sent. */
  readonly uploading: boolean;
  /** How much of the ZIP was sent, from 0 to 1. */
  readonly progress: number;
  /** Why the ZIP was not accepted or did not go up, in words; null when there is nothing to say. */
  readonly problem: string | null;
  /** Opens the picker. */
  choose(): Promise<void>;
  /** Sends the ZIP that was chosen. */
  confirm(): void;
  /** Drops the ZIP that was chosen, or stops the one that is being sent. */
  cancel(): void;
}
