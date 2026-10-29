import type { ContractFlowEvent, ContractFlowState } from '../types/utils/contractFlow.types';
import { isSessionValid } from './proofFlow';

/** The flow before the client starts it. */
export const INITIAL_CONTRACT_FLOW: ContractFlowState = { step: 'closed', maskedEmail: null, session: null };

/**
 * Moves the contract flow to its next state. Opening the flow skips the code when the client already verified one on this page
 * (for example, to upload a payment proof a moment ago).
 * @param state Current state.
 * @param event What happened.
 * @returns The next state.
 */
export function contractFlowReducer(state: ContractFlowState, event: ContractFlowEvent): ContractFlowState {
  switch (event.type) {
    case 'open':
      return isSessionValid(event.session, event.now)
        ? { ...state, step: 'read', session: event.session }
        : { ...state, step: 'request', session: null };
    case 'codeSent':
      return { ...state, step: 'enter', maskedEmail: event.maskedEmail };
    case 'verified':
      return { ...state, step: 'read', session: event.session };
    case 'sessionExpired':
      return { ...state, step: 'request', session: null };
    case 'signed':
      return { ...state, step: 'signed' };
    case 'close':
      return { ...state, step: 'closed' };
  }
}
