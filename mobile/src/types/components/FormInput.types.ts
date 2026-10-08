import type { TextInputProps } from 'react-native';

/** Props of the `FormInput` component; everything else is passed to the underlying `TextInput`. */
export interface FormInputProps extends Omit<TextInputProps, 'style' | 'placeholderTextColor'> {
  /** Label shown above the field. */
  readonly label: string;
  /** Validation message shown below the field, if any. */
  readonly error?: string;
}
