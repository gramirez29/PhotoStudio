/** Values of the login form, as typed. */
export interface LoginFormValues {
  /** Email of the photographer. */
  readonly email: string;
  /** Password of the photographer. */
  readonly password: string;
}

/** Fields of the login form that can show a validation message. */
export type LoginField = keyof LoginFormValues;

/** Validation messages of the login form, by field. */
export type LoginFormErrors = Readonly<Partial<Record<LoginField, string>>>;

/** Result of validating the login form: the credentials to send, or the messages to show. */
export type LoginFormResult =
  | { readonly ok: true; readonly email: string; readonly password: string }
  | { readonly ok: false; readonly errors: LoginFormErrors };
