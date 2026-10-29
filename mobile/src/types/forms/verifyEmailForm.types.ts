import type { VerifyEmailRequest } from '../api/auth';

/** Result of validating the code the user typed: the request to send, or the message to show. */
export type VerifyEmailFormResult =
  | { readonly ok: true; readonly request: VerifyEmailRequest }
  | { readonly ok: false; readonly error: string };
