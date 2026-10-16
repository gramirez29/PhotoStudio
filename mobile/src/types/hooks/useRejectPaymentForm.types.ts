/** State and actions of the reject-payment form, as returned by `useRejectPaymentForm`. */
export interface RejectPaymentFormState {
  /** Reason as typed. */
  readonly text: string;
  /** Validation message of the reason, or null. */
  readonly validationError: string | null;
  /** Message for the last failed request, or null. */
  readonly submitError: string | null;
  /** True while the rejection is being sent. */
  readonly isPending: boolean;
  /** Updates the reason and clears its validation message. */
  readonly setText: (text: string) => void;
  /** Validates the reason and, if it is valid, rejects the proof and goes back. */
  readonly submit: () => void;
}
