/** Props of the `DurationPicker` component. */
export interface DurationPickerProps {
  /** Label shown above the options. */
  readonly label: string;
  /** Durations offered, in hours. */
  readonly options: readonly number[];
  /** Selected duration, in hours. */
  readonly value: number;
  /** Called with the duration the photographer taps. */
  readonly onChange: (hours: number) => void;
}
