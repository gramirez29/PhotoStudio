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
}
