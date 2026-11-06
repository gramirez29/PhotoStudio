import { useState, type ReactElement } from 'react';
import { StyleSheet, View } from 'react-native';
import { HelperText, TextInput } from 'react-native-paper';
import { spacing } from '../theme/tokens';
import type { FormInputProps } from '../types/components/FormInput.types';

/**
 * Outlined Material text field with its label, and its validation message below. A password field (one with `secureTextEntry`) has
 * an eye at its right end: tapping it shows what was typed, to check it, and tapping it again hides it. It starts hidden every time
 * the field appears.
 * @param props Component props; everything else is passed to the underlying text field.
 * @returns The field.
 */
export function FormInput({ label, error, secureTextEntry = false, ...inputProps }: FormInputProps): ReactElement {
  const [revealed, setRevealed] = useState(false);

  return (
    <View style={styles.container}>
      <TextInput
        {...inputProps}
        mode="outlined"
        label={label}
        accessibilityLabel={label}
        error={error !== undefined}
        secureTextEntry={secureTextEntry && !revealed}
        right={
          secureTextEntry ? (
            <TextInput.Icon
              icon={revealed ? 'eye-off' : 'eye'}
              accessibilityLabel={revealed ? 'Ocultar contraseña' : 'Mostrar contraseña'}
              onPress={() => setRevealed((current) => !current)}
            />
          ) : undefined
        }
      />
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
