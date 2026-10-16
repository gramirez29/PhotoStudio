import type { SubmitProofRequest } from '../api/portal';

/** What the client typed in the payment proof form. */
export interface ProofFormValues {
  /** Amount as typed. */
  readonly amount: string;
  /** Reference of the transfer as typed; optional. */
  readonly reference: string;
}

/** Form fields that can show a validation message. */
export type ProofField = keyof ProofFormValues;

/** Validation messages of the proof form, by field. */
export type ProofFormErrors = Readonly<Partial<Record<ProofField, string>>>;

/** Result of validating the proof form: the request to send, or the messages to show. */
export type ProofFormResult =
  | { readonly ok: true; readonly request: SubmitProofRequest }
  | { readonly ok: false; readonly errors: ProofFormErrors };
