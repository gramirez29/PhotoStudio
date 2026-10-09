import type { RecordInPersonPaymentRequest } from '../api/booking';

/** Values of the payment form, as typed or picked. */
export interface PaymentFormValues {
  /** Amount as typed, in colones. */
  readonly amount: string;
  /** Method picked, as a key of the in-person methods. */
  readonly method: RecordInPersonPaymentRequest['method'];
  /** Key that makes sending the payment safe to repeat; created once per form. */
  readonly idempotencyKey: string;
}

/** Result of validating the payment form: the request to send, or the message to show. */
export type PaymentFormResult =
  | { readonly ok: true; readonly request: RecordInPersonPaymentRequest }
  | { readonly ok: false; readonly error: string };
