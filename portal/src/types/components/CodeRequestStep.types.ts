/** Props of the `CodeRequestStep` component. */
export interface CodeRequestStepProps {
  /** Secret token of the booking link. */
  readonly token: string;
  /** What the code is for, worded to follow "Para" (for example "subir tu comprobante"). */
  readonly purpose: string;
  /** Why the client is here again (for example, the verification expired), or null. */
  readonly notice: string | null;
  /** Called with the masked address once the code was sent. */
  readonly onSent: (maskedEmail: string) => void;
  /** Called when the client gives up. */
  readonly onCancel: () => void;
}
