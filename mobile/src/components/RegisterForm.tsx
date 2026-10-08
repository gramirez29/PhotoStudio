import type { ReactElement } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { MIN_PASSWORD_LENGTH } from '../forms/registerForm';
import { useRegisterForm } from '../hooks/useRegisterForm';
import { colors, spacing } from '../theme/tokens';
import { ActionButton } from './ActionButton';
import { FormInput } from './FormInput';

/**
 * Account creation fields and button: name, phone, username and password (typed twice). The account is created and signed
 * in at once, so the navigator replaces the screen by itself.
 * @returns The fields.
 */
export function RegisterForm(): ReactElement {
  const form = useRegisterForm();

  return (
    <View style={styles.container}>
      <FormInput
        label="Nombre"
        value={form.values.name}
        onChangeText={(text) => form.setField('name', text)}
        error={form.errors.name}
        autoCapitalize="words"
        autoComplete="name"
        textContentType="name"
        returnKeyType="next"
        placeholder="Tu nombre"
      />
      <FormInput
        label="Teléfono"
        value={form.values.phone}
        onChangeText={form.setPhone}
        error={form.errors.phone}
        keyboardType="phone-pad"
        autoComplete="tel"
        textContentType="telephoneNumber"
        placeholder="8888-8888"
      />
      <FormInput
        label="Correo"
        value={form.values.email}
        onChangeText={(text) => form.setField('email', text)}
        error={form.errors.email}
        keyboardType="email-address"
        autoCapitalize="none"
        autoCorrect={false}
        autoComplete="email"
        textContentType="emailAddress"
        returnKeyType="next"
        placeholder="tu@correo.com"
      />
      <FormInput
        label="Usuario"
        value={form.values.username}
        onChangeText={(text) => form.setField('username', text)}
        error={form.errors.username}
        autoCapitalize="none"
        autoCorrect={false}
        autoComplete="username-new"
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
        autoComplete="new-password"
        textContentType="newPassword"
        returnKeyType="next"
      />
      <Text style={styles.hint}>Mínimo {MIN_PASSWORD_LENGTH} caracteres.</Text>
      <FormInput
        label="Repite la contraseña"
        value={form.values.confirmPassword}
        onChangeText={(text) => form.setField('confirmPassword', text)}
        error={form.errors.confirmPassword}
        secureTextEntry
        autoCapitalize="none"
        autoCorrect={false}
        autoComplete="new-password"
        textContentType="newPassword"
        returnKeyType="go"
        onSubmitEditing={form.submit}
      />

      {form.submitError !== null && <Text style={styles.submitError}>{form.submitError}</Text>}

      <ActionButton
        label={form.isPending ? 'Creando cuenta…' : 'Crear cuenta'}
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
  hint: {
    color: colors.textSecondary,
    fontSize: 13,
    marginTop: -spacing.sm,
  },
  submitError: {
    color: colors.danger,
    fontSize: 14,
  },
});
