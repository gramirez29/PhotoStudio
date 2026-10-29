import type { PortalSession } from '../types/api/portal';
import type { ProofFlowEvent, ProofFlowState } from '../types/utils/proofFlow.types';

/** A session closer to its expiry than this (in milliseconds) is treated as expired, so a form is never filled to be refused. */
export const SESSION_MARGIN_MS = 60_000;

/** The flow before the client starts it. */
export const INITIAL_PROOF_FLOW: ProofFlowState = { step: 'closed', maskedEmail: null, session: null };

/**
 * Tells whether a session can still be used.
 * @param session Session to check, if any.
 * @param now Current instant in milliseconds since the epoch.
 * @returns True when the session exists and does not expire within the safety margin.
 */
export function isSessionValid(session: PortalSession | null, now: number): boolean {
  return session !== null && Date.parse(session.expiresAt) - now > SESSION_MARGIN_MS;
}

/**
 * Moves the proof flow to its next state. Opening the flow skips the code when the page already has a valid session: the client
 * verified a code a moment ago, here or in the contract flow.
 * @param state Current state.
 * @param event What happened.
 * @returns The next state.
 */
export function proofFlowReducer(state: ProofFlowState, event: ProofFlowEvent): ProofFlowState {
  switch (event.type) {
    case 'open':
      return isSessionValid(event.session, event.now)
        ? { ...state, step: 'form', session: event.session }
        : { ...state, step: 'request', session: null };
    case 'codeSent':
      return { ...state, step: 'enter', maskedEmail: event.maskedEmail };
    case 'verified':
      return { ...state, step: 'form', session: event.session };
    case 'sessionExpired':
      return { ...state, step: 'request', session: null };
    case 'askAgain':
      return { ...state, step: 'request' };
    case 'proofSent':
      return { ...state, step: 'sent' };
    case 'close':
      return { ...state, step: 'closed' };
  }
}
