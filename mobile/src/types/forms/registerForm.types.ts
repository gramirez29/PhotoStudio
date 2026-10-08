import type { RegisterRequest } from '../api/auth';

/** Values of the registration form, as typed. */
export interface RegisterFormValues {
  /** Display name. */
  readonly name: string;
  /** Phone number, shown as 0000-0000 while typed. */
  readonly phone: string;
  /** Email address. */
  readonly email: string;
  /** Username to log in with. */
  readonly username: string;
  /** Password. */
  readonly password: string;
  /** Password typed a second time, to catch typos. */
  readonly confirmPassword: string;
}

/** Fields of the registration form that can show a validation message. */
export type RegisterField = keyof RegisterFormValues;

/** Validation messages of the registration form, by field. */
export type RegisterFormErrors = Readonly<Partial<Record<RegisterField, string>>>;

/** Result of validating the registration form: the request to send, or the messages to show. */
export type RegisterFormResult =
  | { readonly ok: true; readonly request: RegisterRequest }
  | { readonly ok: false; readonly errors: RegisterFormErrors };
