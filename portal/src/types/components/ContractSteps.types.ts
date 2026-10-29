import type { PortalBooking, PortalSession } from '../api/portal';
import type { ContractFormValues } from '../forms/contractForm.types';

/** Props of the `ContractFlow` component. */
export interface ContractFlowProps {
  /** Secret token of the booking link. */
  readonly token: string;
  /** Booking as the API last described it. */
  readonly booking: PortalBooking;
  /** Session the page already has (the client verified a code for another action), or null. */
  readonly session: PortalSession | null;
  /** Called when a code is verified or the session stops working, so the other flows of the page share it. */
  readonly onSessionChange: (session: PortalSession | null) => void;
}

/** Props of the `ContractReadStep` component. */
export interface ContractReadStepProps {
  /** Secret token of the booking link. */
  readonly token: string;
  /** Session obtained by verifying a code. */
  readonly session: PortalSession;
  /** What the client typed so far; it lives in the flow so it survives a verification that expires. */
  readonly values: ContractFormValues;
  /** Called with the new values on every change. */
  readonly onChange: (values: ContractFormValues) => void;
  /** Called once the contract was signed. */
  readonly onSigned: () => void;
  /** Called when the server refuses the session, so the client confirms a code again. */
  readonly onSessionExpired: () => void;
  /** Called when the client closes the contract. */
  readonly onClose: () => void;
}

/** Props of the `ContractSignForm` component. */
export interface ContractSignFormProps {
  /** Secret token of the booking link. */
  readonly token: string;
  /** Session obtained by verifying a code. */
  readonly session: PortalSession;
  /** Version of the contract the client just read. */
  readonly templateVersion: string;
  /** What the client typed so far. */
  readonly values: ContractFormValues;
  /** Called with the new values on every change. */
  readonly onChange: (values: ContractFormValues) => void;
  /** Called once the contract was signed. */
  readonly onSigned: () => void;
  /** Called when the server refuses the session. */
  readonly onSessionExpired: () => void;
  /** Called when the client gives up. */
  readonly onCancel: () => void;
}

/** Props of the `ContractSignedStep` component. */
export interface ContractSignedStepProps {
  /** Called when the client closes the confirmation. */
  readonly onDone: () => void;
}
