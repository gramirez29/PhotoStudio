import type { PaymentResponse } from '../api/booking';

/** Handlers of the payment proofs waiting for verification, as returned by `usePaymentReview`. */
export interface PaymentReviewState {
  /** True while a verification is being sent. */
  readonly isPending: boolean;
  /** Message for the last failed verification, or null. */
  readonly errorMessage: string | null;
  /** Asks for confirmation and, when the photographer accepts, verifies the proof. */
  readonly verify: (payment: PaymentResponse) => void;
  /** Opens the screen to type the reason for rejecting the proof. */
  readonly reject: (payment: PaymentResponse) => void;
}
