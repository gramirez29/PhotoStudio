import type { ReactElement } from 'react';
import { Text } from 'react-native';
import { MIN_PASSWORD_LENGTH } from '../forms/registerForm';
import { useChangePasswordForm } from '../hooks/useChangePasswordForm';
import { ActionButton } from './ActionButton';
import { FormInput } from './FormInput';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * Change-password fields and button: the current password and the new one, typed twice. On success the screen closes by
 * itself and the other devices are signed out.
 * @returns The form.
 */
export function ChangePasswordForm(): ReactElement {
  const styles = useStyles();
  const form = useChangePasswordForm();

  return (
    <KeyboardAwareScreen align="top">
      <FormInput
        label="Contraseña actual"
        value={form.values.currentPassword}
        onChangeText={(text) => form.setField('currentPassword', text)}
        error={form.errors.currentPassword}
        secureTextEntry
        autoCapitalize="none"
        autoCorrect={false}
        autoComplete="current-password"
        textContentType="password"
        returnKeyType="next"
      />
      <FormInput
        label="Contraseña nueva"
        value={form.values.newPassword}
        onChangeText={(text) => form.setField('newPassword', text)}
        error={form.errors.newPassword}
        secureTextEntry
        autoCapitalize="none"
        autoCorrect={false}
        autoComplete="new-password"
        textContentType="newPassword"
        returnKeyType="next"
      />
      <Text style={styles.hint}>Mínimo {MIN_PASSWORD_LENGTH} caracteres.</Text>
      <FormInput
        label="Repite la contraseña nueva"
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
      <Text style={styles.hint}>Al cambiarla se cierra la sesión en tus otros dispositivos.</Text>

      {form.submitError !== null && <Text style={styles.submitError}>{form.submitError}</Text>}

      <ActionButton
        label={form.isPending ? 'Guardando…' : 'Cambiar contraseña'}
        disabled={form.isPending}
        onPress={form.submit}
      />
    </KeyboardAwareScreen>
  );
}

/** Styles of the form. */
const useStyles = makeStyles((palette) => ({
  hint: {
    color: palette.onSurfaceVariant,
    fontSize: 13,
    fontFamily: FONT_FAMILIES.body,
  },
  submitError: {
    color: palette.error,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
}));
