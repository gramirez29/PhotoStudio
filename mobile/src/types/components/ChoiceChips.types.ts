/** Props of the `ChoiceChips` component. */
export interface ChoiceChipsProps {
  /** Label shown above the options. */
  readonly label: string;
  /** Options offered, in the order they are shown. */
  readonly options: readonly string[];
  /** Selected option, or an empty string when none is selected. */
  readonly value: string;
  /** Called with the option the photographer taps. */
  readonly onChange: (option: string) => void;
  /** Validation message shown below the options, if any. */
  readonly error?: string;
}
