import type { RegisterField, RegisterFormErrors, RegisterFormValues } from '../forms/registerForm.types';

/** State and actions of the registration form, as returned by `useRegisterForm`. */
export interface RegisterFormState {
  /** Current values of the form. */
  readonly values: RegisterFormValues;
  /** Validation messages by field. */
  readonly errors: RegisterFormErrors;
  /** True while the account is being created. */
  readonly isPending: boolean;
  /** Message for the last failed request, or null. */
  readonly submitError: string | null;
  /** Updates a field and clears its validation message. */
  readonly setField: (field: RegisterField, text: string) => void;
  /** Updates the phone, formatted as 0000-0000, and clears its validation message. */
  readonly setPhone: (text: string) => void;
  /** Validates the form and, if it is valid, creates the account and signs in. */
  readonly submit: () => void;
}
