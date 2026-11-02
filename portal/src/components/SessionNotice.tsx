import type { ReactElement } from 'react';
import type { SessionNoticeProps } from '../types/components/SessionNotice.types';
import { formatDay } from '../utils/format';

/**
 * Tells the client that their email is already confirmed in this device, until when, and that they will not be asked for another code,
 * and offers to close it, for a device that is not theirs. It shows nothing while there is no session.
 * @param props Component props.
 * @returns The notice, or nothing.
 */
export function SessionNotice({ session, onClose }: SessionNoticeProps): ReactElement | null {
  if (session === null) {
    return null;
  }

  return (
    <p className="session-notice muted" role="status">
      Tu correo ya está confirmado en este dispositivo hasta el {formatDay(session.expiresAt)}: no te pediremos otro código.{' '}
      <button type="button" className="session-notice__close" onClick={onClose}>
        Cerrar sesión en este dispositivo
      </button>
    </p>
  );
}
