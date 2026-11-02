/** One final photo about to be uploaded, as the API asks for it. */
export interface FinalFileInfo {
  /** Identifier of the chosen photo the final belongs to (the identifier of its proof). */
  readonly photoId: string;
  /** Size of the file in bytes. */
  readonly bytes: number;
}
