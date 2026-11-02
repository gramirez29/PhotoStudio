/** One file the client is about to download. */
export interface DownloadItem {
  /** Signed address of the file. It is a secret that expires: it is never logged. */
  readonly url: string;
  /** Name to save the file with. */
  readonly fileName: string;
}

/** The photos of a selection, split in what the package covers and what is beyond it. */
export interface SplitChoice {
  /** The first photos chosen, as many as the package covers. */
  readonly packageIds: readonly string[];
  /** The photos chosen beyond the package, in the order they were chosen. */
  readonly extraIds: readonly string[];
}
