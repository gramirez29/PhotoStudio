import type {
  RecoverPasswordField,
  RecoverPasswordFormErrors,
  RecoverPasswordFormValues,
} from '../forms/recoverPasswordForm.types';

/** Step of the recovery: first the account, then the code and the new password. */
export type RecoverPasswordStep = 'request' | 'confirm';

/** State and actions of the password recovery, as returned by `useRecoverPasswordForm`. */
export interface RecoverPasswordFormState {
  /** Step the user is on. */
  readonly step: RecoverPasswordStep;
  /** Current values of the form. */
  readonly values: RecoverPasswordFormValues;
  /** Validation messages by field. */
  readonly errors: RecoverPasswordFormErrors;
  /** True while a request is in flight. */
  readonly isPending: boolean;
  /** Message for the last failed request, or null. */
  readonly submitError: string | null;
  /** Message that tells the user to look at their email, or null. */
  readonly notice: string | null;
  /** Seconds the user has to wait before asking for another code; zero when they may. */
  readonly cooldownSeconds: number;
  /** Updates a field and clears its validation message. */
  readonly setField: (field: RecoverPasswordField, text: string) => void;
  /** Validates the account and asks for the code. */
  readonly requestCode: () => void;
  /** Validates the code and the new password and changes the password. */
  readonly confirm: () => void;
  /** Asks for another code for the same account. */
  readonly resendCode: () => void;
  /** Goes back to the first step to type another account. */
  readonly changeAccount: () => void;
}
