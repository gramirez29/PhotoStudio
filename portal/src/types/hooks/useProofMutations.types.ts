import type { SubmitProofRequest } from '../api/portal';

/** What the mutation that registers a payment proof needs. */
export interface SubmitProofVariables {
  /** Token of the portal session obtained by verifying a code. */
  readonly accessToken: string;
  /** Amount, reference and idempotency key. */
  readonly request: SubmitProofRequest;
}
