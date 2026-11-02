/** What the gallery screen runs when the photographer taps its actions. Each one that changes something asks to confirm first. */
export interface GalleryActionHandlers {
  /** True while a command is running. */
  readonly isPending: boolean;
  /** Why the last command failed, in words; null when it did not. */
  readonly errorMessage: string | null;
  /** Opens the selection, after confirming: the client is told by email and the proofs can no longer be changed. */
  openSelection(): void;
  /** Publishes the final photos of the package, after confirming. */
  publishFinals(): void;
  /** Lets the client download the extras, after asking whether they already paid. */
  releaseExtras(): void;
  /** Deletes the ZIP, after confirming. */
  removeZip(): void;
  /**
   * Removes a proof, after confirming.
   * @param photoId Proof identifier.
   */
  removeProof(photoId: string): void;
}
