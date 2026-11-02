import type { PortalSession } from '../api/portal';

/** Props of the `SessionNotice` component. */
export interface SessionNoticeProps {
  /** The session of the link, or null while the client has not confirmed their email. */
  readonly session: PortalSession | null;
  /** Called when the client closes the session in this device. */
  readonly onClose: () => void;
}
