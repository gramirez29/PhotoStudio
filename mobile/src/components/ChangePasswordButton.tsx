import type { ReactElement } from 'react';
import { Pressable, Text } from 'react-native';
import { openChangePassword } from '../navigation/appNavigation';
import { spacing } from '../theme/tokens';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * Entry to the change-password form. It is a quiet text link, because it is used rarely and must not compete with the
 * actions on bookings.
 * @returns The button.
 */
export function ChangePasswordButton(): ReactElement {
  const styles = useStyles();
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
const useStyles = makeStyles((palette) => ({
  button: {
    alignSelf: 'flex-start',
    marginBottom: spacing.md,
  },
  pressed: {
    opacity: 0.6,
  },
  label: {
    color: palette.onSurfaceVariant,
    fontSize: 14,
    textDecorationLine: 'underline',
    fontFamily: FONT_FAMILIES.bodyMedium,
  },
}));
