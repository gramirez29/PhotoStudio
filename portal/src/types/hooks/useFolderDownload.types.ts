import type { FolderDownloadFailure } from '../media/folderDownload.types';

/** Where the saving of the finals of a gallery is. */
export type FolderDownloadPhase = 'idle' | 'running' | 'done' | 'started' | 'problem';

/** The saving of the finals of a gallery and what can be done with it. */
export interface FolderDownload {
  /** Where it is: nothing yet, running, finished and verified, started one by one (not verifiable), or finished with problems. */
  readonly phase: FolderDownloadPhase;
  /** How many finals were handled so far. */
  readonly handled: number;
  /** How many finals there are to save. */
  readonly total: number;
  /** How many were received with the recorded size and written. */
  readonly saved: number;
  /** The finals that were not saved. */
  readonly failed: readonly FolderDownloadFailure[];
  /** Why the saving could not start or went wrong as a whole, in words; null when there is nothing to say. */
  readonly problem: string | null;
  /** Whether this browser can save into a folder; otherwise the files are downloaded one by one. */
  readonly canUseFolder: boolean;
  /** Starts (or starts again) the saving. */
  start(): void;
}
