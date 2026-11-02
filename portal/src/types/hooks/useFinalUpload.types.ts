import type { FinalUploadItem } from '../media/finalUploader.types';
import type { FinalMatch } from '../utils/finalMatching.types';

/** What the page needs to run an upload of finals: what is staged, the queue, where it is, and what the person can do. */
export interface FinalUpload {
  /** The files paired with their photo that wait for the person to say go. */
  readonly staged: readonly FinalMatch[];
  /** The finals that are not registered yet: waiting, in progress, or failed. */
  readonly items: readonly FinalUploadItem[];
  /** True while finals are being sent and registered. */
  readonly running: boolean;
  /** Why the run stopped, in words, when a request failed; otherwise null. */
  readonly problem: string | null;
  /** Which files were left out when the person chose them, in words; null when none was. */
  readonly notice: string | null;
  /**
   * Pairs the files the person chose with the photos of the selection and stages them until they confirm.
   * @param files The files chosen.
   */
  choose(files: readonly File[]): void;
  /** Starts uploading what was staged. */
  confirm(): void;
  /** Drops what was staged without uploading it. */
  cancel(): void;
  /** Tries again with every final that failed or was left waiting. */
  retry(): void;
  /** Takes the failed finals out of the queue. */
  dismissFailed(): void;
}
