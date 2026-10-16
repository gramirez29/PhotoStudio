import type { PortalSession } from '../api/portal';

/** Props of the `CodeEntryStep` component. */
export interface CodeEntryStepProps {
  /** Secret token of the booking link. */
  readonly token: string;
  /** Address the code went to, partly hidden. */
  readonly maskedEmail: string;
  /** Called with the session once the code was accepted. */
  readonly onVerified: (session: PortalSession) => void;
  /** Called when the client gives up. */
  readonly onCancel: () => void;
}
