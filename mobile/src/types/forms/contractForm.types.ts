import type { CONTRACT_SIGNATURE_KINDS } from '../../constants/contract';
import type { SignContractInPersonRequest } from '../api/booking';

/** How the contract is signed in person. */
export type ContractSignatureKind = (typeof CONTRACT_SIGNATURE_KINDS)[number];

/** Result of validating the contract form: the request to send, or the messages to show. */
export type ContractFormResult =
  | { readonly ok: true; readonly request: SignContractInPersonRequest }
  | { readonly ok: false; readonly error: string };
