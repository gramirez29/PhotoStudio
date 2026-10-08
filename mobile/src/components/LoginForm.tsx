import type { ReactElement } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { useLoginForm } from '../hooks/useLoginForm';
import { colors, spacing } from '../theme/tokens';
import { ActionButton } from './ActionButton';
import { FormInput } from './FormInput';

/**
 * Sign-in fields and button: username and password. The navigator replaces the screen by itself once the session is in place.
 * @returns The fields.
 */
export function LoginForm(): ReactElement {
  const form = useLoginForm();

  return (
    <View style={styles.container}>
      <FormInput
        label="Usuario"
        value={form.values.username}
        onChangeText={(text) => form.setField('username', text)}
        error={form.errors.username}
        autoCapitalize="none"
        autoCorrect={false}
        autoComplete="username"
        textContentType="username"
        returnKeyType="next"
        placeholder="tu.usuario"
      />
      <FormInput
        label="Contraseña"
        value={form.values.password}
        onChangeText={(text) => form.setField('password', text)}
        error={form.errors.password}
        secureTextEntry
        autoCapitalize="none"
        autoCorrect={false}
        autoComplete="current-password"
        textContentType="password"
        returnKeyType="go"
        onSubmitEditing={form.submit}
      />

      {form.submitError !== null && <Text style={styles.submitError}>{form.submitError}</Text>}

      <ActionButton
        label={form.isPending ? 'Entrando…' : 'Iniciar sesión'}
        disabled={form.isPending}
        onPress={form.submit}
      />
    </View>
  );
}

/** Styles of the fields. */
const styles = StyleSheet.create({
  container: {
    gap: spacing.md,
  },
  submitError: {
    color: colors.danger,
    fontSize: 14,
  },
});
