import type { CONTRACT_SIGNATURE_KINDS } from '../../constants/contract';
import type { ImageUse, SignContractInPersonRequest } from '../api/booking';

/** Way of recording the signature: the client signs on the phone, or the contract is a paper one. */
export type ContractSignatureKind = (typeof CONTRACT_SIGNATURE_KINDS)[number];

/** What the photographer and the client typed and chose in the contract screen. */
export interface ContractFormValues {
  /** Way of recording the signature. */
  readonly kind: ContractSignatureKind;
  /** Name of the person who signs, as typed. */
  readonly signerName: string;
  /** Identity number as typed; hyphens and spaces are allowed. Only used when the client signs on the phone. */
  readonly idNumber: string;
  /** Number of people as typed. Only used when the client signs on the phone. */
  readonly participantCount: string;
  /** Place of the session as typed; optional. Only used when the client signs on the phone. */
  readonly sessionLocation: string;
  /** The image use option the client chose, or null while they have not chosen. */
  readonly imageUse: ImageUse | null;
  /** Whether the client confirmed they read the contract and accept it. Only used when the client signs on the phone. */
  readonly accepted: boolean;
}

/** Form fields that can show a validation message. */
export type ContractField = keyof ContractFormValues;

/** Validation messages of the contract form, by field. */
export type ContractFormErrors = Readonly<Partial<Record<ContractField, string>>>;

/** Result of validating the contract form: the request to send, or the messages to show. */
export type ContractFormResult =
  | { readonly ok: true; readonly request: SignContractInPersonRequest }
  | { readonly ok: false; readonly errors: ContractFormErrors };
