/** One final the photographer is about to save: which photo it belongs to, the name of its original and its size. */
export interface FolderDownloadItem {
  /** Identifier of the photo. */
  readonly photoId: string;
  /** Name of the original file, for messages. */
  readonly name: string;
  /** Size the API recorded for the final, in bytes; what is received must match it. */
  readonly bytes: number;
}

/** A final that could not be saved, and why. */
export interface FolderDownloadFailure {
  /** Identifier of the photo. */
  readonly photoId: string;
  /** Name of the original file. */
  readonly name: string;
  /** Why: the size received was not the recorded one, or the file could not be received or written. */
  readonly reason: 'size' | 'failed';
}

/** What the saving of the finals needs from outside; tests give others. */
export interface FolderDownloadDeps {
  /**
   * Asks for the signed address and the file name of a final.
   * @param photoId Identifier of the photo.
   * @returns The address and the name to save it with.
   */
  readonly getLink: (photoId: string) => Promise<{ readonly url: string; readonly fileName: string }>;
  /**
   * Receives a file from its signed address.
   * @param url The address.
   * @returns The file; rejects when the storage does not answer with success.
   */
  readonly fetchFile: (url: string) => Promise<Blob>;
  /**
   * Writes a file in the folder the photographer chose.
   * @param fileName Name of the file inside the folder.
   * @param file The content.
   * @returns A promise that resolves when it is written.
   */
  readonly writeFile: (fileName: string, file: Blob) => Promise<void>;
}

/** How a saving of finals ended. */
export interface FolderDownloadResult {
  /** Finals received with the size the API recorded and written to the folder. */
  readonly saved: number;
  /** Finals that were not saved. */
  readonly failed: readonly FolderDownloadFailure[];
}

/** The part of the folder picker of the browser that the page uses. */
export interface FolderWriter {
  /**
   * Writes a file in the folder.
   * @param fileName Name of the file inside the folder.
   * @param file The content.
   * @returns A promise that resolves when it is written.
   */
  writeFile(fileName: string, file: Blob): Promise<void>;
}
