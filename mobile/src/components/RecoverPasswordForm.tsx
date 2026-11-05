import type { ReactElement } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useRecoverPasswordForm } from '../hooks/useRecoverPasswordForm';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';
import type { RecoverPasswordFormProps } from '../types/components/RecoverPasswordForm.types';
import { ActionButton } from './ActionButton';
import { FormInput } from './FormInput';

/**
 * Recovers a forgotten password in two steps: the user names their account and asks for a code, which goes to the verified email of
 * the account, and then types the code and the new password. It never says whether the account exists. Nobody is signed in by
 * recovering; once the password changes the screen goes back to the login.
 * @param props Component props.
 * @param props.onRecovered Called once the password was changed.
 * @returns The fields and buttons of the current step.
 */
export function RecoverPasswordForm({ onRecovered }: RecoverPasswordFormProps): ReactElement {
  const styles = useStyles();
  const form = useRecoverPasswordForm(onRecovered);
  const waiting = form.cooldownSeconds > 0;
  const resendDisabled = waiting || form.isPending;

  if (form.step === 'request') {
    return (
      <View style={styles.container}>
        <Text style={styles.body}>
          Escribe tu usuario o tu correo. Te enviaremos un código para elegir una contraseña nueva, siempre que el correo de la cuenta esté
          verificado.
        </Text>
        <FormInput
          label="Usuario o correo"
          value={form.values.identifier}
          onChangeText={(text) => form.setField('identifier', text)}
          error={form.errors.identifier}
          autoCapitalize="none"
          autoCorrect={false}
          autoComplete="username"
          returnKeyType="go"
          onSubmitEditing={form.requestCode}
        />

        {form.submitError !== null && <Text style={styles.submitError}>{form.submitError}</Text>}

        <ActionButton label={form.isPending ? 'Enviando…' : 'Enviar código'} disabled={form.isPending} onPress={form.requestCode} />
      </View>
    );
  }

  let resendLabel = 'Enviar un código nuevo';
  if (waiting) {
    resendLabel = `Enviar un código nuevo (${form.cooldownSeconds} s)`;
  }

  return (
    <View style={styles.container}>
      {form.notice !== null && <Text style={styles.body}>{form.notice}</Text>}

      <FormInput
        label="Código de 6 dígitos"
        value={form.values.code}
        onChangeText={(text) => form.setField('code', text)}
        error={form.errors.code}
        keyboardType="number-pad"
        autoCapitalize="none"
        autoCorrect={false}
        autoComplete="one-time-code"
        textContentType="oneTimeCode"
        maxLength={6}
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
        onSubmitEditing={form.confirm}
      />

      {form.submitError !== null && <Text style={styles.submitError}>{form.submitError}</Text>}

      <ActionButton label={form.isPending ? 'Cambiando…' : 'Cambiar contraseña'} disabled={form.isPending} onPress={form.confirm} />

      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Enviar un código nuevo"
        accessibilityState={{ disabled: resendDisabled }}
        disabled={resendDisabled}
        onPress={form.resendCode}
        style={({ pressed }) => [styles.link, pressed && styles.pressed]}
      >
        <Text style={[styles.linkLabel, resendDisabled && styles.linkLabelDisabled]}>{resendLabel}</Text>
      </Pressable>

      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Usar otra cuenta"
        disabled={form.isPending}
        onPress={form.changeAccount}
        style={({ pressed }) => [styles.link, pressed && styles.pressed]}
      >
        <Text style={styles.linkLabel}>Usar otra cuenta</Text>
      </Pressable>
    </View>
  );
}

/** Styles of the form. */
const useStyles = makeStyles((palette) => ({
  container: {
    gap: spacing.md,
  },
  body: {
    color: palette.onSurfaceVariant,
    fontSize: 15,
    fontFamily: FONT_FAMILIES.body,
  },
  submitError: {
    color: palette.error,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
  link: {
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: 44,
    paddingVertical: spacing.sm,
  },
  linkLabel: {
    color: palette.onSurface,
    fontSize: 15,
    textDecorationLine: 'underline',
    fontFamily: FONT_FAMILIES.bodyMedium,
  },
  linkLabelDisabled: {
    color: palette.muted,
    textDecorationLine: 'none',
  },
  pressed: {
    opacity: 0.6,
  },
}));
