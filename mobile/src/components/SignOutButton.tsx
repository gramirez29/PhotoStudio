import type { ReactElement } from 'react';
import { Alert, Pressable, StyleSheet, Text } from 'react-native';
import { signOut } from '../session/sessionManager';
import { colors, spacing } from '../theme/tokens';

/**
 * Header button that signs the photographer out after a confirmation. The navigator takes them to the login screen by itself.
 * @returns The button.
 */
export function SignOutButton(): ReactElement {
  /** Asks for confirmation before ending the session. */
  function confirmSignOut(): void {
    Alert.alert('Cerrar sesión', '¿Quieres cerrar la sesión en este dispositivo?', [
      { text: 'Cancelar', style: 'cancel' },
      { text: 'Cerrar sesión', style: 'destructive', onPress: () => void signOut() },
    ]);
  }

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel="Cerrar sesión"
      hitSlop={spacing.sm}
      onPress={confirmSignOut}
      style={({ pressed }) => pressed && styles.pressed}
    >
      <Text style={styles.label}>Salir</Text>
    </Pressable>
  );
}

/** Styles of the button. */
const styles = StyleSheet.create({
  label: {
    color: colors.textSecondary,
    fontSize: 16,
    fontWeight: '600',
  },
  pressed: {
    opacity: 0.6,
  },
});
