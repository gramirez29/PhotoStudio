import { useState, type ReactElement } from 'react';
import { Pressable, StyleSheet, Text } from 'react-native';
import { colors, spacing } from '../theme/tokens';
import type { AuthMode } from '../types/components/AuthPanel.types';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';
import { LoginForm } from './LoginForm';
import { RegisterForm } from './RegisterForm';

/**
 * The content of the sign-in screen: the login form, or the account creation form one tap away. Switching resets what was
 * typed, since each form owns its own state.
 * @returns The panel.
 */
export function AuthPanel(): ReactElement {
  const [mode, setMode] = useState<AuthMode>('login');
  const isLogin = mode === 'login';

  return (
    <KeyboardAwareScreen>
      <Text style={styles.title}>PhotoStud.io</Text>
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
const styles = StyleSheet.create({
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
  switch: {
    alignItems: 'center',
    paddingVertical: spacing.sm,
  },
  pressed: {
    opacity: 0.6,
  },
  switchLabel: {
    color: colors.primary,
    fontSize: 15,
    fontWeight: '600',
  },
});
