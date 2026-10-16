import type { ChangePasswordRequest } from '../api/auth';

/** Values of the change-password form, as typed. */
export interface ChangePasswordFormValues {
  /** Password the user has now. */
  readonly currentPassword: string;
  /** Password the user wants. */
  readonly newPassword: string;
  /** New password typed a second time, to catch typos. */
  readonly confirmPassword: string;
}

/** Fields of the change-password form that can show a validation message. */
export type ChangePasswordField = keyof ChangePasswordFormValues;

/** Validation messages of the change-password form, by field. */
export type ChangePasswordFormErrors = Readonly<Partial<Record<ChangePasswordField, string>>>;

/** Result of validating the change-password form: the request to send, or the messages to show. */
export type ChangePasswordFormResult =
  | { readonly ok: true; readonly request: ChangePasswordRequest }
  | { readonly ok: false; readonly errors: ChangePasswordFormErrors };
