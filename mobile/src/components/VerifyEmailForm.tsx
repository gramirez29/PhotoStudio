import type { ReactElement } from 'react';
import { Pressable, StyleSheet, Text } from 'react-native';
import { useVerifyEmailForm } from '../hooks/useVerifyEmailForm';
import { colors, spacing } from '../theme/tokens';
import { ActionButton } from './ActionButton';
import { FormInput } from './FormInput';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';

/**
 * Screen content that asks the user to prove they own their email: it says where the code went, takes the six digits, and lets
 * them ask for another code. The app shows only this until the email is verified, so nothing else is reachable meanwhile.
 * @returns The form.
 */
export function VerifyEmailForm(): ReactElement {
  const form = useVerifyEmailForm();
  const waiting = form.cooldownSeconds > 0;
  const resendDisabled = waiting || form.isSending;

  let resendLabel = 'Enviar un código nuevo';
  if (form.isSending) {
    resendLabel = 'Enviando…';
  } else if (waiting) {
    resendLabel = `Enviar un código nuevo (${form.cooldownSeconds} s)`;
  }

  return (
    <KeyboardAwareScreen align="top">
      <Text style={styles.title}>Verifica tu correo</Text>
      <Text style={styles.body}>
        Para empezar a usar la app, escribe el código de 6 dígitos que enviamos a {form.maskedEmail}. Si no lo ves, revisa la carpeta
        de correo no deseado.
      </Text>

      <FormInput
        label="Código de 6 dígitos"
        value={form.code}
        onChangeText={form.setCode}
        error={form.codeError}
        keyboardType="number-pad"
        autoCapitalize="none"
        autoCorrect={false}
        autoComplete="one-time-code"
        textContentType="oneTimeCode"
        maxLength={6}
        returnKeyType="go"
        onSubmitEditing={form.verify}
      />

      {form.notice !== null && <Text style={styles.notice}>{form.notice}</Text>}
      {form.submitError !== null && <Text style={styles.submitError}>{form.submitError}</Text>}

      <ActionButton label={form.isVerifying ? 'Verificando…' : 'Verificar'} disabled={form.isVerifying} onPress={form.verify} />

      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Enviar un código nuevo"
        accessibilityState={{ disabled: resendDisabled }}
        disabled={resendDisabled}
        onPress={form.resend}
        style={({ pressed }) => [styles.link, pressed && styles.pressed]}
      >
        <Text style={[styles.linkLabel, resendDisabled && styles.linkLabelDisabled]}>{resendLabel}</Text>
      </Pressable>
    </KeyboardAwareScreen>
  );
}

/** Styles of the form. */
const styles = StyleSheet.create({
  title: {
    color: colors.textPrimary,
    fontSize: 22,
    fontWeight: '700',
  },
  body: {
    color: colors.textSecondary,
    fontSize: 15,
  },
  notice: {
    color: colors.textPrimary,
    fontSize: 14,
  },
  submitError: {
    color: colors.danger,
    fontSize: 14,
  },
  link: {
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: 44,
    paddingVertical: spacing.sm,
  },
  linkLabel: {
    color: colors.textPrimary,
    fontSize: 15,
    fontWeight: '600',
    textDecorationLine: 'underline',
  },
  linkLabelDisabled: {
    color: colors.muted,
    textDecorationLine: 'none',
  },
  pressed: {
    opacity: 0.6,
  },
});
