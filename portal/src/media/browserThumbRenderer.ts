import type { ThumbRenderer } from '../types/media/thumbRenderer.types';
import { createBrowserCanvas, decodeInBrowser } from './browserCanvas';
import { createThumbRenderer } from './thumbRenderer';

/** The renderer of thumbnails, wired to the canvas of the browser. */
export const browserThumbRenderer: ThumbRenderer = createThumbRenderer({ decode: decodeInBrowser, createCanvas: createBrowserCanvas });
