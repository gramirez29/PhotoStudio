import type { LoginField, LoginFormErrors, LoginFormValues } from '../forms/loginForm.types';

/** Credentials sent to the backend by the login form. */
export interface LoginCredentials {
  /** Username, trimmed and in lower case. */
  readonly username: string;
  /** Password, exactly as typed. */
  readonly password: string;
}

/** State and actions of the login form, as returned by `useLoginForm`. */
export interface LoginFormState {
  /** Current values of the form. */
  readonly values: LoginFormValues;
  /** Validation messages by field. */
  readonly errors: LoginFormErrors;
  /** True while the credentials are being checked. */
  readonly isPending: boolean;
  /** Message for the last failed sign-in, or null. */
  readonly submitError: string | null;
  /** Updates a field and clears its validation message. */
  readonly setField: (field: LoginField, text: string) => void;
  /** Validates the form and, if it is valid, signs in. */
  readonly submit: () => void;
}
