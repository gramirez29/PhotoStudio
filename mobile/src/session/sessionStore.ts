import { create } from 'zustand';
import type { SessionStore } from '../types/session/session.types';

/**
 * State of the photographer's session: where the app is in its sign-in lifecycle and, when signed in, the access token and
 * who it belongs to. It holds no side effects; the session manager changes it as the network and the storage answer.
 */
export const useSessionStore = create<SessionStore>((set) => ({
  status: 'restoring',
  session: null,
  setSignedIn: (session) => set({ status: 'signedIn', session }),
  setSignedOut: () => set({ status: 'signedOut', session: null }),
  setOffline: () => set({ status: 'offline', session: null }),
  setRestoring: () => set({ status: 'restoring', session: null }),
}));
