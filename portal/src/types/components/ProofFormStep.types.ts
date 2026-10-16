import type { PortalBooking, PortalSession } from '../api/portal';
import type { ProofFormValues } from '../forms/proofForm.types';

/** Props of the `ProofFormStep` component. */
export interface ProofFormProps {
  /** Secret token of the booking link. */
  readonly token: string;
  /** Booking the proof is for; its balance bounds the amount. */
  readonly booking: PortalBooking;
  /** Session obtained by verifying a code. */
  readonly session: PortalSession;
  /** What the client has typed so far. */
  readonly values: ProofFormValues;
  /** Key of this proof, so repeating the send registers it once. */
  readonly idempotencyKey: string;
  /** Called with the new values on every change. */
  readonly onChange: (values: ProofFormValues) => void;
  /** Called once the proof was registered. */
  readonly onSent: () => void;
  /** Called when the verification expired and the client has to confirm a code again. */
  readonly onSessionExpired: () => void;
  /** Called when the client gives up. */
  readonly onCancel: () => void;
}
