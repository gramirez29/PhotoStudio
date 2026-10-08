/** Result of validating a reason: the reason to send, or the message to show. */
export type ReasonFormResult =
  | { readonly ok: true; readonly reason: string | null }
  | { readonly ok: false; readonly error: string };
