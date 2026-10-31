/** The sample proof shown before an upload: how the proofs will look. */
export interface ProofSample {
  /** Whether the sample is being made, is ready, or could not be made. */
  readonly state: 'loading' | 'ready' | 'failed';
  /** Temporary address of the sample, when it is ready; otherwise null. */
  readonly url: string | null;
}
