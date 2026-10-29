import type { PortalSession } from '../api/portal';

/**
 * Where the client is in the payment proof flow.
 * - `closed`: only the button to start is shown.
 * - `request`: asks to send the verification code.
 * - `enter`: the code was sent; waits for the client to type it.
 * - `form`: the client is verified; the proof form is shown.
 * - `sent`: the proof was registered.
 */
export type ProofStep = 'closed' | 'request' | 'enter' | 'form' | 'sent';

/** State of the proof flow. */
export interface ProofFlowState {
  /** Current step. */
  readonly step: ProofStep;
  /** Address the code went to, partly hidden, once it was sent. */
  readonly maskedEmail: string | null;
  /** Session obtained by verifying a code. It lives in memory only and is dropped when it expires. */
  readonly session: PortalSession | null;
}

/** Something that happens in the proof flow. */
export type ProofFlowEvent =
  | { readonly type: 'open'; readonly now: number; readonly session: PortalSession | null }
  | { readonly type: 'codeSent'; readonly maskedEmail: string }
  | { readonly type: 'verified'; readonly session: PortalSession }
  | { readonly type: 'sessionExpired' }
  | { readonly type: 'askAgain' }
  | { readonly type: 'proofSent' }
  | { readonly type: 'close' };
