/** How a failed load of the booking is explained to the client. */
export interface LoadErrorView {
  /** Short title. */
  readonly title: string;
  /** Explanation and what to do. */
  readonly message: string;
  /** Whether trying again can help; false for a link that is not valid. */
  readonly canRetry: boolean;
}
