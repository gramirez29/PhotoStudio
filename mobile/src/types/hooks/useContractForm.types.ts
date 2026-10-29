import type { ContractField, ContractFormErrors, ContractFormValues, ContractSignatureKind } from '../forms/contractForm.types';

/** What `useContractForm` gives the screen that records the signature of a contract. */
export interface ContractFormState {
  /** What was typed and chosen so far. */
  readonly values: ContractFormValues;
  /** Validation messages by field. */
  readonly errors: ContractFormErrors;
  /** Message of the last failed signature, or null. */
  readonly submitError: string | null;
  /** Whether the signature is being sent. */
  readonly isPending: boolean;
  /** Whether the client has to read the contract on the phone: the signature is not a paper one. */
  readonly showsContract: boolean;
  /** Text of the contract the client reads, or null while it loads or when it could not be loaded. */
  readonly contractText: string | null;
  /** Whether the contract is still loading. */
  readonly isContractLoading: boolean;
  /** Message when the contract could not be loaded, or null. */
  readonly contractError: string | null;
  /** Loads the contract again after a failure. */
  readonly reloadContract: () => void;
  /** Sets the way of recording the signature. */
  readonly setKind: (kind: ContractSignatureKind) => void;
  /** Sets the text of a field, and clears its validation message. */
  readonly setText: (field: 'signerName' | 'idNumber' | 'participantCount' | 'sessionLocation', text: string) => void;
  /** Sets the image use the client chose, or the acceptance of the contract. */
  readonly setChoice: <Field extends Extract<ContractField, 'imageUse' | 'accepted'>>(field: Field, value: ContractFormValues[Field]) => void;
  /** Validates the form and sends the signature. */
  readonly submit: () => void;
}
