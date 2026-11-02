/** The storage refused a file that was sent to a signed address, or the sending failed. */
export class StorageUploadError extends Error {
  /** HTTP status the storage answered, or null when there was no answer (no connection, or the upload was interrupted). */
  public readonly status: number | null;

  /**
   * Creates the error.
   * @param status HTTP status the storage answered, or null when there was none.
   */
  public constructor(status: number | null) {
    super(status === null ? 'The file could not be sent to the storage.' : `The storage refused the file (status ${status}).`);
    this.name = 'StorageUploadError';
    this.status = status;
  }
}
