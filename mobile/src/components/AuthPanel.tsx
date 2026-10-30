import { useState, type ReactElement } from 'react';
import { Pressable, Text } from 'react-native';
import { spacing } from '../theme/tokens';
import type { AuthMode } from '../types/components/AuthPanel.types';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';
import { LoginForm } from './LoginForm';
import { RegisterForm } from './RegisterForm';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * The content of the sign-in screen: the login form, or the account creation form one tap away. Switching resets what was
 * typed, since each form owns its own state.
 * @returns The panel.
 */
export function AuthPanel(): ReactElement {
  const styles = useStyles();
  const [mode, setMode] = useState<AuthMode>('login');
  const isLogin = mode === 'login';

  return (
    <KeyboardAwareScreen>
      <Text style={styles.title}>PhotoSense Studio</Text>
      <Text style={styles.subtitle}>
        {isLogin ? 'Inicia sesión para ver y administrar tus reservas.' : 'Crea tu cuenta para empezar a administrar tus reservas.'}
      </Text>

      {isLogin ? <LoginForm /> : <RegisterForm />}

      <Pressable
        accessibilityRole="button"
        hitSlop={spacing.sm}
        onPress={() => setMode(isLogin ? 'register' : 'login')}
        style={({ pressed }) => [styles.switch, pressed && styles.pressed]}
      >
        <Text style={styles.switchLabel}>{isLogin ? '¿No tienes cuenta? Crear cuenta' : '¿Ya tienes cuenta? Iniciar sesión'}</Text>
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
