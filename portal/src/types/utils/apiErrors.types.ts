/** How a failed load of the booking is explained to the client. */
export interface LoadErrorView {
  /** Short title. */
  readonly title: string;
  /** Explanation and what to do. */
  readonly message: string;
  /** Whether trying again can help; false for a link that is not valid. */
  readonly canRetry: boolean;
}

/** How a failed action of the proof flow (ask for a code, verify it, send the proof) is explained to the client. */
export interface ActionErrorView {
  /** What went wrong and what to do. */
  readonly message: string;
  /** Seconds to wait before asking again, when the server said so; otherwise null. */
  readonly retryAfterSeconds: number | null;
  /** True when the verification expired, so the client has to confirm a code again. */
  readonly sessionExpired: boolean;
}
