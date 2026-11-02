/** One photo the viewer can show. */
export interface ViewerPhoto {
  /** Photo identifier. */
  readonly id: string;
  /** Name shown under the photo. */
  readonly name: string;
  /** What is known about it, in words (size, date), or an empty text. */
  readonly caption: string;
  /** A small image to show while the real photo loads, or when it cannot be loaded; null when there is none. */
  readonly placeholder: string | null;
}

/** Props of the `PhotoViewer` component. */
export interface PhotoViewerProps {
  /** The photos the viewer moves between. */
  readonly photos: readonly ViewerPhoto[];
  /** Position of the photo that is shown. */
  readonly index: number;
  /**
   * Gets the address of the real photo, in the quality it was edited. It rejects when it cannot be had.
   * @param photoId Identifier of the photo.
   * @returns The address.
   */
  readonly loadSource: (photoId: string) => Promise<string>;
  /** Called with the position of the photo the person moves to. */
  readonly onMove: (index: number) => void;
  /** Called when the person closes the viewer. */
  readonly onClose: () => void;
}
