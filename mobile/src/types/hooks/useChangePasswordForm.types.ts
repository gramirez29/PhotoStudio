import type {
  ChangePasswordField,
  ChangePasswordFormErrors,
  ChangePasswordFormValues,
} from '../forms/changePasswordForm.types';

/** State and actions of the change-password form, as returned by `useChangePasswordForm`. */
export interface ChangePasswordFormState {
  /** Current values of the form. */
  readonly values: ChangePasswordFormValues;
  /** Validation messages by field. */
  readonly errors: ChangePasswordFormErrors;
  /** True while the password is being changed. */
  readonly isPending: boolean;
  /** Message for the last failed request, or null. */
  readonly submitError: string | null;
  /** Updates a field and clears its validation message. */
  readonly setField: (field: ChangePasswordField, text: string) => void;
  /** Validates the form and, if it is valid, changes the password. */
  readonly submit: () => void;
}
