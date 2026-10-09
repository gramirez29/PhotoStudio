import type { ContractSignatureKind } from '../forms/contractForm.types';

/** State and actions of the contract form, as returned by `useContractForm`. */
export interface ContractFormState {
  /** Name of the person who signs, as typed. */
  readonly signerName: string;
  /** How the contract is signed. */
  readonly kind: ContractSignatureKind;
  /** Validation message, or null. */
  readonly validationError: string | null;
  /** Message for the last failed request, or null. */
  readonly submitError: string | null;
  /** True while the signature is being sent. */
  readonly isPending: boolean;
  /** Updates the signer name and clears the validation message. */
  readonly setSignerName: (name: string) => void;
  /** Updates how the contract is signed. */
  readonly setKind: (kind: ContractSignatureKind) => void;
  /** Validates the form and, if it is valid, records the signature and goes back. */
  readonly submit: () => void;
}
