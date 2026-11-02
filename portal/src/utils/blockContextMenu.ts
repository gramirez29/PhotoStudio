import type { MouseEvent } from 'react';

/**
 * Stops the context menu of the browser on a proof, so "save image as" is not one click away. It is a weak layer, and the page does
 * not pretend otherwise: what protects the photographer is that the proof has low quality and a big watermark in the middle.
 * @param event Mouse event of the context menu.
 */
export function blockContextMenu(event: MouseEvent): void {
  event.preventDefault();
}
