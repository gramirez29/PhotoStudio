/** Values of the login form, as typed. */
export interface LoginFormValues {
  /** Username of the user. */
  readonly username: string;
  /** Password of the user. */
  readonly password: string;
}

/** Fields of the login form that can show a validation message. */
export type LoginField = keyof LoginFormValues;

/** Validation messages of the login form, by field. */
export type LoginFormErrors = Readonly<Partial<Record<LoginField, string>>>;

/** Result of validating the login form: the credentials to send, or the messages to show. */
export type LoginFormResult =
  | { readonly ok: true; readonly username: string; readonly password: string }
  | { readonly ok: false; readonly errors: LoginFormErrors };
