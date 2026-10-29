import type { PortalSession } from '../api/portal';

/**
 * Where the client is in the contract flow.
 * - `closed`: only the button to start is shown.
 * - `request`: asks to send the verification code.
 * - `enter`: the code was sent; waits for the client to type it.
 * - `read`: the client is verified; the contract is shown, with the form when it is unsigned.
 * - `signed`: the contract was just signed.
 */
export type ContractStep = 'closed' | 'request' | 'enter' | 'read' | 'signed';

/** State of the contract flow. */
export interface ContractFlowState {
  /** Current step. */
  readonly step: ContractStep;
  /** Address the code went to, partly hidden, once it was sent. */
  readonly maskedEmail: string | null;
  /** Session obtained by verifying a code. It lives in memory only and is dropped when it expires. */
  readonly session: PortalSession | null;
}

/** Something that happens in the contract flow. */
export type ContractFlowEvent =
  | { readonly type: 'open'; readonly now: number; readonly session: PortalSession | null }
  | { readonly type: 'codeSent'; readonly maskedEmail: string }
  | { readonly type: 'verified'; readonly session: PortalSession }
  | { readonly type: 'sessionExpired' }
  | { readonly type: 'signed' }
  | { readonly type: 'close' };
