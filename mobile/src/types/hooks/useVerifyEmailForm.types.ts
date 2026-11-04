/** What `useVerifyEmailForm` gives the screen that verifies the email. */
export interface VerifyEmailFormState {
  /** Address the code goes to, partly hidden. */
  readonly maskedEmail: string;
  /** Digits typed so far. */
  readonly code: string;
  /** Validation message of the code, or undefined. */
  readonly codeError: string | undefined;
  /** Whether the code is being checked. */
  readonly isVerifying: boolean;
  /** Whether a new code is being sent. */
  readonly isSending: boolean;
  /** Message of the last failed check or shipment, or null. */
  readonly submitError: string | null;
  /** Confirmation that a new code was sent, or null. */
  readonly notice: string | null;
  /** Seconds the user has to wait before asking for another code; zero when they may. */
  readonly cooldownSeconds: number;
  /** Called with the text of the field on every change. */
  readonly setCode: (text: string) => void;
  /** Checks the code. */
  readonly verify: () => void;
  /** Sends a new code to the email of the account. */
  readonly resend: () => void;
  /** Whether the user is correcting the address instead of typing the code. */
  readonly isChangingEmail: boolean;
  /** Address typed in the correction. */
  readonly newEmail: string;
  /** Validation message of the address, or undefined. */
  readonly newEmailError: string | undefined;
  /** Whether the correction is being sent. */
  readonly isSavingEmail: boolean;
  /** Opens the correction, with the current address ready to edit. */
  readonly openEmailChange: () => void;
  /** Closes the correction and goes back to the code. */
  readonly closeEmailChange: () => void;
  /** Called with the text of the address field on every change. */
  readonly setNewEmail: (text: string) => void;
  /** Sends the corrected address and, when the backend accepts it, a code goes there. */
  readonly saveEmail: () => void;
}
