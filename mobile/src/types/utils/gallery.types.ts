/** How a state of a gallery is shown. */
export interface GalleryStatusView {
  /** Short name of the state. */
  readonly label: string;
  /** What the photographer does next, in a sentence. */
  readonly hint: string;
  /** Which color of the palette it takes. */
  readonly tone: 'success' | 'warning' | 'info' | 'muted';
}
