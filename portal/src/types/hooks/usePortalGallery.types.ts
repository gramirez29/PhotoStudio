/** What the mutation that asks for the addresses to download some photos needs. */
export interface RequestDownloadsVariables {
  /** Token of the session obtained by verifying a code. */
  readonly accessToken: string;
  /** Identifiers of the photos the client wants. */
  readonly photoIds: readonly string[];
}

/** What the mutation that sends the selection needs. */
export interface SubmitSelectionVariables {
  /** Token of the session obtained by verifying a code. */
  readonly accessToken: string;
  /** Identifiers of the photos chosen, in the order they were chosen. */
  readonly photoIds: readonly string[];
  /** True when the client accepted that the studio contacts them to agree on the payment of the photos beyond the package. */
  readonly acceptExtras: boolean;
}
