import type { ChangeEmailRequest } from '../api/auth';

/** Result of validating the address the user typed: the request to send, or the message to show. */
export type ChangeEmailFormResult =
  | { readonly ok: true; readonly request: ChangeEmailRequest }
  | { readonly ok: false; readonly error: string };
