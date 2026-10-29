import type { ReactElement } from 'react';
import { StyleSheet, View } from 'react-native';
import { HelperText, TextInput } from 'react-native-paper';
import { spacing } from '../theme/tokens';
import type { FormInputProps } from '../types/components/FormInput.types';

/**
 * Outlined Material text field with its label, and its validation message below.
 * @param props Component props; everything else is passed to the underlying text field.
 * @returns The field.
 */
export function FormInput({ label, error, ...inputProps }: FormInputProps): ReactElement {
  return (
    <View style={styles.container}>
      <TextInput {...inputProps} mode="outlined" label={label} accessibilityLabel={label} error={error !== undefined} />
      {error !== undefined && (
        <HelperText type="error" visible>
          {error}
        </HelperText>
      )}
    </View>
  );
}

/** Layout of the field; its colors come from the theme. */
const styles = StyleSheet.create({
  container: {
    gap: spacing.xs,
  },
});
