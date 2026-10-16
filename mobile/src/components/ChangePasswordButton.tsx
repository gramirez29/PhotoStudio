import type { ReactElement } from 'react';
import { Pressable, StyleSheet, Text } from 'react-native';
import { openChangePassword } from '../navigation/appNavigation';
import { colors, spacing } from '../theme/tokens';

/**
 * Entry to the change-password form. It is a quiet text link, because it is used rarely and must not compete with the
 * actions on bookings.
 * @returns The button.
 */
export function ChangePasswordButton(): ReactElement {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel="Cambiar contraseña"
      onPress={openChangePassword}
      hitSlop={spacing.sm}
      style={({ pressed }) => [styles.button, pressed && styles.pressed]}
    >
      <Text style={styles.label}>Cambiar contraseña</Text>
    </Pressable>
  );
}

/** Styles of the button. */
const styles = StyleSheet.create({
  button: {
    alignSelf: 'flex-start',
    marginBottom: spacing.md,
  },
  pressed: {
    opacity: 0.6,
  },
  label: {
    color: colors.textSecondary,
    fontSize: 14,
    fontWeight: '600',
    textDecorationLine: 'underline',
  },
});
