import type { Palette } from '../theme/palette.types';

/** How the readiness of the backend is shown: a sentence and the role of the palette that colors it. */
export interface ApiHealthDescription {
  /** Sentence shown to the photographer. */
  readonly label: string;
  /** Role of the palette that colors the sentence; the theme decides the color. */
  readonly color: keyof Palette;
}
