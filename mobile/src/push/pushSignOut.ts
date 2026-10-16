import { signOut } from '../session/sessionManager';
import { unregisterPushDevice } from './pushManager';

/**
 * Signs out of this device: first tells the backend to stop pushing to this phone (it needs the session that is about to
 * end), then forgets the session. The first step is best effort and time-bounded, so signing out works without a connection.
 * @returns A promise that resolves when the session is gone.
 */
export async function signOutOfDevice(): Promise<void> {
  await unregisterPushDevice();
  await signOut();
}
