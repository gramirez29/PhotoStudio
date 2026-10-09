import type { CompleteRefundRequest } from '../api/billing';

/** State and actions of the refund form, as returned by `useRefundForm`. */
export interface RefundFormState {
  /** Method picked. */
  readonly method: CompleteRefundRequest['method'];
  /** Note as typed. */
  readonly note: string;
  /** Validation message, or null. */
  readonly validationError: string | null;
  /** Message for the last failed request, or null. */
  readonly submitError: string | null;
  /** True while the refund is being recorded. */
  readonly isPending: boolean;
  /** Updates the method. */
  readonly setMethod: (method: CompleteRefundRequest['method']) => void;
  /** Updates the note and clears the validation message. */
  readonly setNote: (note: string) => void;
  /** Validates the form and, if it is valid, records the refund and goes back. */
  readonly submit: () => void;
}
