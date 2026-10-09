import type { CompleteRefundRequest } from '../api/billing';

/** Result of validating the refund form: the request to send, or the message to show. */
export type RefundFormResult =
  | { readonly ok: true; readonly request: CompleteRefundRequest }
  | { readonly ok: false; readonly error: string };
