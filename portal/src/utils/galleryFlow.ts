import type { GalleryFlowEvent, GalleryFlowState } from '../types/utils/gallerySelection.types';
import { isSessionValid } from './proofFlow';

/** The flow before the client starts it. */
export const INITIAL_GALLERY_FLOW: GalleryFlowState = { step: 'closed', maskedEmail: null, session: null };

/**
 * Moves the gallery flow to its next state. Opening the flow skips the code when the page already has a valid session: the client
 * verified a code a moment ago, for the contract or for a payment.
 * @param state Current state.
 * @param event What happened.
 * @returns The next state.
 */
export function galleryFlowReducer(state: GalleryFlowState, event: GalleryFlowEvent): GalleryFlowState {
  switch (event.type) {
    case 'open':
      return isSessionValid(event.session, event.now)
        ? { ...state, step: 'choose', session: event.session }
        : { ...state, step: 'request', session: null };
    case 'codeSent':
      return { ...state, step: 'enter', maskedEmail: event.maskedEmail };
    case 'verified':
      return { ...state, step: 'choose', session: event.session };
    case 'sessionExpired':
      return { ...state, step: 'request', session: null };
    case 'close':
      return { ...state, step: 'closed' };
  }
}
