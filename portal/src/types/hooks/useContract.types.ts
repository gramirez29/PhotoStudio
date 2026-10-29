import type { SignContractRequest } from '../api/portal';

/** What the mutation that signs the contract needs. */
export interface SignContractVariables {
  /** Token of the session obtained by verifying a code. */
  readonly accessToken: string;
  /** What the client declared and accepted. */
  readonly request: SignContractRequest;
}
