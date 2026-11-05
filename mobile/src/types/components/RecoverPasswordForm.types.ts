/** Props of `RecoverPasswordForm`. */
export interface RecoverPasswordFormProps {
  /** Called once the password was changed, so the screen can go back to the login. */
  readonly onRecovered: () => void;
}
