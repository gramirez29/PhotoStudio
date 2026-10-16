/** Result of validating the reason to reject a payment proof: the reason to send, or the message to show. */
export type RejectPaymentFormResult =
  | { readonly ok: true; readonly reason: string }
  | { readonly ok: false; readonly error: string };
