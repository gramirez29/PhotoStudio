/** Props of the `DateTimeField` component. */
export interface DateTimeFieldProps {
  /** Label shown above the field. */
  readonly label: string;
  /** Selected instant, in the device time zone. */
  readonly value: Date;
  /** Called with the new instant when the photographer picks a day or a time. */
  readonly onChange: (next: Date) => void;
  /** Earliest day that can be picked. */
  readonly minimumDate: Date;
  /** Validation message shown below the field, if any. */
  readonly error?: string;
}

/** Part of the instant that a picker edits. */
export type PickerMode = 'date' | 'time';
