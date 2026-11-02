/** Where the upload of the ZIP is. */
export type ZipUploadPhase = 'idle' | 'uploading' | 'registering';

/** What the page needs to upload the ZIP: where it is, why it failed, and what the person can do. */
export interface ZipUpload {
  /** Where the upload is. */
  readonly phase: ZipUploadPhase;
  /** Why the last attempt failed, in words; null when it did not. */
  readonly problem: string | null;
  /**
   * Checks a file and, if it can be a ZIP, sends it straight to the storage and registers it.
   * @param file The file the person chose.
   * @returns A promise that resolves when the attempt ended, successfully or not.
   */
  upload(file: File): Promise<void>;
}
