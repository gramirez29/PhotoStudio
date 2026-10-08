import type { ReactElement } from 'react';
import { StyleSheet, Text, TextInput, View } from 'react-native';
import { colors, radius, spacing } from '../theme/tokens';
import type { FormInputProps } from '../types/components/FormInput.types';

/**
 * Labeled text field that shows its validation message below.
 * @param props Component props; everything else is passed to the underlying `TextInput`.
 * @returns The field.
 */
export function FormInput({ label, error, ...inputProps }: FormInputProps): ReactElement {
  return (
    <View style={styles.container}>
      <Text style={styles.label}>{label}</Text>
      <TextInput
        {...inputProps}
        accessibilityLabel={label}
        placeholderTextColor={colors.muted}
        style={[styles.input, error !== undefined && styles.inputError]}
      />
      {error !== undefined && <Text style={styles.error}>{error}</Text>}
    </View>
  );
}

/** Styles of the field. */
const styles = StyleSheet.create({
  container: {
    gap: spacing.xs,
  },
  label: {
    color: colors.textPrimary,
    fontSize: 14,
    fontWeight: '600',
  },
  input: {
    borderColor: colors.border,
    borderRadius: radius.sm,
    borderWidth: 1,
    color: colors.textPrimary,
    fontSize: 16,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
  },
  inputError: {
    borderColor: colors.danger,
  },
  error: {
    color: colors.danger,
    fontSize: 13,
  },
});
