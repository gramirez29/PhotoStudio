/** Props of the `TextField` component. */
export interface TextFieldProps {
  /** Identifier of the input; ties the label, the hint and the error to it. */
  readonly id: string;
  /** Visible label. */
  readonly label: string;
  /** Current value. */
  readonly value: string;
  /** Called with the new text on every change. */
  readonly onChange: (value: string) => void;
  /** Validation message; the field is marked invalid while there is one. */
  readonly error?: string | undefined;
  /** Help shown under the field. */
  readonly hint?: string;
  /** Kind of input; a password hides what is typed. Defaults to text. */
  readonly type?: 'text' | 'password';
  /** Keyboard the phone shows. */
  readonly inputMode?: 'numeric' | 'text';
  /** What the browser may autofill. */
  readonly autoComplete?: string;
  /** Most characters accepted. */
  readonly maxLength?: number;
  /** Whether the field takes the focus when it appears. */
  readonly autoFocus?: boolean;
  /** Text shown while the field is empty. */
  readonly placeholder?: string;
  /** Fixed text before the value, such as a currency symbol. */
  readonly prefix?: string;
}
