import type { PasswordResetConfirmRequest, PasswordResetRequest } from '../api/auth';

/** Values of the form that recovers a forgotten password, as typed. */
export interface RecoverPasswordFormValues {
  /** Username or email of the account. */
  readonly identifier: string;
  /** The six digits sent to the email of the account. */
  readonly code: string;
  /** Password the user wants. */
  readonly newPassword: string;
  /** New password typed a second time, to catch typos. */
  readonly confirmPassword: string;
}

/** Fields of the recovery form that can show a validation message. */
export type RecoverPasswordField = keyof RecoverPasswordFormValues;

/** Validation messages of the recovery form, by field. */
export type RecoverPasswordFormErrors = Readonly<Partial<Record<RecoverPasswordField, string>>>;

/** Result of validating the first step: the request to send, or the messages to show. */
export type RequestResetFormResult =
  | { readonly ok: true; readonly request: PasswordResetRequest }
  | { readonly ok: false; readonly errors: RecoverPasswordFormErrors };

/** Result of validating the second step: the request to send, or the messages to show. */
export type ConfirmResetFormResult =
  | { readonly ok: true; readonly request: PasswordResetConfirmRequest }
  | { readonly ok: false; readonly errors: RecoverPasswordFormErrors };
