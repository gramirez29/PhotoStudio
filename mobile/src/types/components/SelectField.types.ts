/** Props of the `SelectField` component. */
export interface SelectFieldProps {
  /** Label shown above the field. */
  readonly label: string;
  /** Options offered in the list, in the order they are shown. */
  readonly options: readonly string[];
  /** Selected option, or an empty string when none is selected. */
  readonly value: string;
  /** Called with the option the photographer picks. */
  readonly onChange: (option: string) => void;
  /** Text shown in the field while nothing is selected. */
  readonly placeholder: string;
  /** Validation message shown below the field, if any. */
  readonly error?: string;
}
