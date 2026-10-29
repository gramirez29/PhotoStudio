import type { ImageUse, SignContractRequest } from '../api/portal';

/** What the client typed and chose in the contract form. */
export interface ContractFormValues {
  /** Name of the signer as typed. */
  readonly signerName: string;
  /** Identity number as typed; hyphens and spaces are allowed. */
  readonly idNumber: string;
  /** Number of people as typed. */
  readonly participantCount: string;
  /** Place of the session as typed; optional. */
  readonly sessionLocation: string;
  /** The image use option the client chose, or null while they have not chosen. */
  readonly imageUse: ImageUse | null;
  /** Whether the client ticked the box that says they read and accept the contract. */
  readonly acceptsTerms: boolean;
}

/** Form fields that can show a validation message. */
export type ContractField = keyof ContractFormValues;

/** Validation messages of the contract form, by field. */
export type ContractFormErrors = Readonly<Partial<Record<ContractField, string>>>;

/** Result of validating the contract form: the request to send, or the messages to show. */
export type ContractFormResult =
  | { readonly ok: true; readonly request: SignContractRequest }
  | { readonly ok: false; readonly errors: ContractFormErrors };
