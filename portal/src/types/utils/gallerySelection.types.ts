import type { Money } from '../api/portal';
import type { PortalSession } from '../api/portal';

/** Where the client is in what they are choosing, worked out from how many photos they picked. */
export interface ChoiceSummary {
  /** Photos picked so far. */
  readonly chosen: number;
  /** Photos the client can pick without paying more. */
  readonly selectable: number;
  /** Included photos still to pick; zero once the package is used up. */
  readonly remaining: number;
  /** Photos picked beyond what the package includes. */
  readonly extraPhotos: number;
  /** What those extra photos cost: the price times how many. */
  readonly extraTotal: Money;
}

/** The lines that tell the client where their choice is. */
export interface ChoiceWords {
  /** How many included photos are left, or that the package is used up. */
  readonly headline: string;
  /** What the extra photos cost, or null while there are none. */
  readonly extra: string | null;
  /** The invitation to add another photo, with its price, or null while there are included photos left. */
  readonly invitation: string | null;
}

/** What the card of the gallery says, for the client, about where the gallery is. */
export interface GalleryCardWords {
  /** Short text under the title. */
  readonly text: string;
  /** Text of the button, or null when there is nothing to open. */
  readonly action: string | null;
}

/**
 * Where the client is in the gallery flow.
 * - `closed`: only the card of the gallery is shown.
 * - `request`: asks to send the verification code.
 * - `enter`: the code was sent; waits for the client to type it.
 * - `choose`: the client is verified; the gallery is open.
 */
export type GalleryStep = 'closed' | 'request' | 'enter' | 'choose';

/** State of the gallery flow. */
export interface GalleryFlowState {
  /** Current step. */
  readonly step: GalleryStep;
  /** Address the code went to, partly hidden, once it was sent. */
  readonly maskedEmail: string | null;
  /** Session obtained by verifying a code. It lives in memory only and is dropped when it expires. */
  readonly session: PortalSession | null;
}

/** Something that happens in the gallery flow. */
export type GalleryFlowEvent =
  | { readonly type: 'open'; readonly now: number; readonly session: PortalSession | null }
  | { readonly type: 'codeSent'; readonly maskedEmail: string }
  | { readonly type: 'verified'; readonly session: PortalSession }
  | { readonly type: 'sessionExpired' }
  | { readonly type: 'close' };
