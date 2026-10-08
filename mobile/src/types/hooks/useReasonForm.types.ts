/** State and actions of the reason form, as returned by `useReasonForm`. */
export interface ReasonFormState {
  /** Reason as typed. */
  readonly text: string;
  /** Validation message, or null. */
  readonly validationError: string | null;
  /** Message for the last failed request, or null. */
  readonly submitError: string | null;
  /** True while the action is being sent. */
  readonly isPending: boolean;
  /** Updates the reason and clears the validation message. */
  readonly setText: (text: string) => void;
  /** Validates the reason and, if it is valid, performs the action and goes back. */
  readonly submit: () => void;
}
