/** What the sign-in form gives the mutation. */
export interface StudioLoginVariables {
  /** Username as typed. */
  readonly username: string;
  /** Password as typed. */
  readonly password: string;
}

/** What the code form gives the mutation. */
export interface StudioVerifyVariables {
  /** The six digits. */
  readonly code: string;
  /** Whether to skip the code in this browser for 30 days. */
  readonly rememberDevice: boolean;
}
