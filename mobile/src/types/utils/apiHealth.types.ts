import type { ColorToken } from '../theme/tokens.types';

/** How the readiness of the backend is shown: a sentence and the color token that goes with it. */
export interface ApiHealthDescription {
  /** Sentence shown to the photographer. */
  readonly label: string;
  /** Color token of the sentence. */
  readonly color: ColorToken;
}
