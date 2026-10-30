import type { StatusTone } from './labels.types';

/** How a gallery state is shown: the words and the tone of the dot. */
export interface GalleryStatusView {
  /** Text of the state. */
  readonly label: string;
  /** Tone of the dot next to the text. */
  readonly tone: StatusTone;
}

/** What the page remembers about a code that is waiting, so a page that reloads continues at the code and not at the password. */
export interface PendingCode {
  /** Where the code went, with the middle hidden. */
  readonly maskedEmail: string;
}
