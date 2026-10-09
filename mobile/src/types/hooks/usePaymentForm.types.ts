import type { RecordInPersonPaymentRequest } from '../api/booking';

/** State and actions of the payment form, as returned by `usePaymentForm`. */
export interface PaymentFormState {
  /** Amount as typed, in colones. */
  readonly amount: string;
  /** Method picked. */
  readonly method: RecordInPersonPaymentRequest['method'];
  /** Validation message, or null. */
  readonly validationError: string | null;
  /** Message for the last failed request, or null. */
  readonly submitError: string | null;
  /** True while the payment is being sent. */
  readonly isPending: boolean;
  /** Updates the amount and clears the validation message. */
  readonly setAmount: (amount: string) => void;
  /** Updates the method. */
  readonly setMethod: (method: RecordInPersonPaymentRequest['method']) => void;
  /** Validates the form and, if it is valid, records the payment and goes back. */
  readonly submit: () => void;
}
