/** What the mutation that sends the selection needs. */
export interface SubmitSelectionVariables {
  /** Token of the session obtained by verifying a code. */
  readonly accessToken: string;
  /** Identifiers of the photos chosen, in the order they were chosen. */
  readonly photoIds: readonly string[];
}
