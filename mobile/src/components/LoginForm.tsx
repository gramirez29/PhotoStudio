import type { ReactElement } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, Text } from 'react-native';
import { useLoginForm } from '../hooks/useLoginForm';
import { colors, spacing } from '../theme/tokens';
import { ActionButton } from './ActionButton';
import { FormInput } from './FormInput';

/**
 * Sign-in form: email and password. The navigator replaces the screen by itself once the session is in place.
 * @returns The form.
 */
export function LoginForm(): ReactElement {
  const form = useLoginForm();

  return (
    <KeyboardAvoidingView style={styles.flex} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
        <Text style={styles.title}>PhotoStud.io</Text>
        <Text style={styles.subtitle}>Inicia sesión para ver y administrar tus reservas.</Text>

        <FormInput
          label="Email"
          value={form.values.email}
          onChangeText={(text) => form.setField('email', text)}
          error={form.errors.email}
          keyboardType="email-address"
          autoCapitalize="none"
          autoCorrect={false}
          autoComplete="email"
          textContentType="username"
          returnKeyType="next"
          placeholder="tu@correo.com"
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
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

/** Styles of the form. */
const styles = StyleSheet.create({
  flex: {
    flex: 1,
  },
  container: {
    flexGrow: 1,
    gap: spacing.md,
    justifyContent: 'center',
    padding: spacing.lg,
  },
  title: {
    color: colors.textPrimary,
    fontSize: 28,
    fontWeight: '700',
  },
  subtitle: {
    color: colors.textSecondary,
    fontSize: 15,
    marginBottom: spacing.sm,
  },
  submitError: {
    color: colors.danger,
    fontSize: 14,
  },
});
