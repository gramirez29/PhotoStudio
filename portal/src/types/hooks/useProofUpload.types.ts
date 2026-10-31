import type { UploadItem } from '../media/proofUploader.types';

/** What the page needs to run an upload of proofs: the queue, where it is, and what the person can do. */
export interface ProofUpload {
  /** The photos that are not registered yet: waiting, in progress, or failed. */
  readonly items: readonly UploadItem[];
  /** True while photos are being prepared, sent and registered. */
  readonly running: boolean;
  /** Why the run stopped, in words, when a request failed; otherwise null. */
  readonly problem: string | null;
  /** Which files were left out when the person chose them, in words; null when none was. */
  readonly notice: string | null;
  /** Files chosen and waiting for the person to see the sample and say go. */
  readonly pending: readonly File[];

  /**
   * Sorts the files the person chose and stages the ones that can be uploaded, until they confirm.
   * @param files The files chosen.
   */
  choose(files: readonly File[]): void;

  /** Starts uploading what was staged (and anything that failed before). */
  confirm(): void;

  /** Drops what was staged without uploading it. */
  cancel(): void;

  /** Tries again with every photo that failed or was left waiting. */
  retry(): void;

  /** Takes the failed photos out of the queue. */
  dismissFailed(): void;
}
