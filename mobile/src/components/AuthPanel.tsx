import { useState, type ReactElement } from 'react';
import { Pressable, Text } from 'react-native';
import { spacing } from '../theme/tokens';
import type { AuthMode } from '../types/components/AuthPanel.types';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';
import { LoginForm } from './LoginForm';
import { RecoverPasswordForm } from './RecoverPasswordForm';
import { RegisterForm } from './RegisterForm';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/** What the screen says under the title in each mode. */
const SUBTITLES: Readonly<Record<AuthMode, string>> = {
  login: 'Inicia sesión para ver y administrar tus reservas.',
  register: 'Crea tu cuenta para empezar a administrar tus reservas.',
  recover: 'Recupera el acceso a tu cuenta.',
};

/** Label of the main link under the form in each mode. */
const SWITCH_LABELS: Readonly<Record<AuthMode, string>> = {
  login: '¿No tienes cuenta? Crear cuenta',
  register: '¿Ya tienes cuenta? Iniciar sesión',
  recover: 'Volver a iniciar sesión',
};

/** Form the main link under each form leads to. */
const SWITCH_TARGETS: Readonly<Record<AuthMode, AuthMode>> = {
  login: 'register',
  register: 'login',
  recover: 'login',
};

/** What the screen says after the password was recovered, above the login. */
const RECOVERED_NOTICE = 'Tu contraseña cambió y se cerraron tus sesiones. Inicia sesión con la nueva.';

/**
 * The content of the sign-in screen: the login form, the account creation form one tap away, and the recovery of a forgotten
 * password. Switching resets what was typed, since each form owns its own state.
 * @returns The panel.
 */
export function AuthPanel(): ReactElement {
  const styles = useStyles();
  const [mode, setMode] = useState<AuthMode>('login');
  const [recovered, setRecovered] = useState(false);

  /**
   * Switches to another form and forgets that the password was just recovered.
   * @param next Form to show.
   */
  const show = (next: AuthMode): void => {
    setRecovered(false);
    setMode(next);
  };

  return (
    <KeyboardAwareScreen>
      <Text style={styles.title}>PhotoSense Studio</Text>
      <Text style={styles.subtitle}>{SUBTITLES[mode]}</Text>

      {mode === 'login' && recovered && <Text style={styles.notice}>{RECOVERED_NOTICE}</Text>}
      {mode === 'login' && <LoginForm />}
      {mode === 'register' && <RegisterForm />}
      {mode === 'recover' && (
        <RecoverPasswordForm
          onRecovered={() => {
            setRecovered(true);
            setMode('login');
          }}
        />
      )}

      {mode === 'login' && (
        <Pressable
          accessibilityRole="button"
          hitSlop={spacing.sm}
          onPress={() => show('recover')}
          style={({ pressed }) => [styles.switch, pressed && styles.pressed]}
        >
          <Text style={styles.switchLabel}>¿Olvidaste tu contraseña?</Text>
        </Pressable>
      )}

      <Pressable
        accessibilityRole="button"
        hitSlop={spacing.sm}
        onPress={() => show(SWITCH_TARGETS[mode])}
        style={({ pressed }) => [styles.switch, pressed && styles.pressed]}
      >
        <Text style={styles.switchLabel}>{SWITCH_LABELS[mode]}</Text>
      </Pressable>
    </KeyboardAwareScreen>
  );
}

/** Styles of the panel. */
const useStyles = makeStyles((palette) => ({
  title: {
    color: palette.onSurface,
    fontSize: 28,
    fontFamily: FONT_FAMILIES.titleBold,
  },
  subtitle: {
    color: palette.onSurfaceVariant,
    fontSize: 15,
    marginBottom: spacing.sm,
    fontFamily: FONT_FAMILIES.body,
  },
  notice: {
    color: palette.onSurface,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.bodyMedium,
  },
  switch: {
    alignItems: 'center',
    paddingVertical: spacing.sm,
  },
  pressed: {
    opacity: 0.6,
  },
  switchLabel: {
    color: palette.primary,
    fontSize: 15,
    fontFamily: FONT_FAMILIES.bodyMedium,
  },
}));
