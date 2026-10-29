import type { TextInputProps } from 'react-native-paper';

/** Props of the `FormInput` component; everything else is passed to the underlying Material text field. */
export interface FormInputProps
  extends Omit<TextInputProps, 'style' | 'mode' | 'label' | 'error' | 'theme' | 'left' | 'right' | 'outlineColor' | 'activeOutlineColor' | 'textColor'> {
  /** Label of the field: it sits inside the field and moves to its border when the field has a value or the focus. */
  readonly label: string;
  /** Validation message shown below the field, if any. */
  readonly error?: string;
}
