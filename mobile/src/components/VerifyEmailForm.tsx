import type { ReactElement } from 'react';
import { Pressable, Text } from 'react-native';
import { useVerifyEmailForm } from '../hooks/useVerifyEmailForm';
import { spacing } from '../theme/tokens';
import { ActionButton } from './ActionButton';
import { FormInput } from './FormInput';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * Screen content that asks the user to prove they own their email: it says where the code went, takes the six digits, and lets
 * them ask for another code or correct the address when it was misspelled. The app shows only this until the email is verified, so nothing else is reachable meanwhile.
 * @returns The form.
 */
export function VerifyEmailForm(): ReactElement {
  const styles = useStyles();
  const form = useVerifyEmailForm();
  const waiting = form.cooldownSeconds > 0;
  const resendDisabled = waiting || form.isSending;

  let resendLabel = 'Enviar un código nuevo';
  if (form.isSending) {
    resendLabel = 'Enviando…';
  } else if (waiting) {
    resendLabel = `Enviar un código nuevo (${form.cooldownSeconds} s)`;
  }

  if (form.isChangingEmail) {
    return (
      <KeyboardAwareScreen align="top">
        <Text style={styles.title}>Corrige tu correo</Text>
        <Text style={styles.body}>
          Escribe el correo correcto y te enviaremos el código ahí. Solo puedes cambiarlo mientras no lo hayas verificado.
        </Text>

        <FormInput
          label="Correo"
          value={form.newEmail}
          onChangeText={form.setNewEmail}
          error={form.newEmailError}
          keyboardType="email-address"
          autoCapitalize="none"
          autoCorrect={false}
          autoComplete="email"
          textContentType="emailAddress"
          returnKeyType="send"
          onSubmitEditing={form.saveEmail}
        />

        {form.submitError !== null && <Text style={styles.submitError}>{form.submitError}</Text>}

        <ActionButton label={form.isSavingEmail ? 'Enviando…' : 'Enviar código'} disabled={form.isSavingEmail} onPress={form.saveEmail} />

        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Cancelar"
          accessibilityState={{ disabled: form.isSavingEmail }}
          disabled={form.isSavingEmail}
          onPress={form.closeEmailChange}
          style={({ pressed }) => [styles.link, pressed && styles.pressed]}
        >
          <Text style={styles.linkLabel}>Cancelar</Text>
        </Pressable>
      </KeyboardAwareScreen>
    );
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

      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Cambiar el correo"
        onPress={form.openEmailChange}
        style={({ pressed }) => [styles.link, pressed && styles.pressed]}
      >
        <Text style={styles.linkLabel}>¿Te equivocaste de correo? Cámbialo</Text>
      </Pressable>
    </KeyboardAwareScreen>
  );
}

/** Styles of the form. */
const useStyles = makeStyles((palette) => ({
  title: {
    color: palette.onSurface,
    fontSize: 22,
    fontFamily: FONT_FAMILIES.titleBold,
  },
  body: {
    color: palette.onSurfaceVariant,
    fontSize: 15,
    fontFamily: FONT_FAMILIES.body,
  },
  notice: {
    color: palette.onSurface,
    fontSize: 14,
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
