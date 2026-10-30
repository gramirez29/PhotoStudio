import { MaterialIcons } from '@expo/vector-icons';
import type { ReactElement } from 'react';
import { Pressable } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import { confirmSignOut } from '../session/confirmSignOut';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';

/** Size of the icon, in points. */
const ICON_SIZE = 26;

/**
 * Header button that signs the photographer out after a confirmation: an icon of a door with an arrow. It is on the screen that
 * asks to verify the email, where there is no menu. It sits on the band of the brand, so its color is the one for that band. The icon is a drawing: the name of the button is what the screen reader reads.
 * The navigator takes them to the login screen by itself.
 * @returns The button.
 */
export function SignOutButton(): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel="Cerrar sesión"
      hitSlop={spacing.md}
      onPress={confirmSignOut}
      style={({ pressed }) => [styles.button, pressed && styles.pressed]}
    >
      <MaterialIcons name="logout" size={ICON_SIZE} color={palette.onBrand} accessible={false} importantForAccessibility="no" />
    </Pressable>
  );
}

/** Styles of the button. */
const useStyles = makeStyles(() => ({
  button: {
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: 40,
    minWidth: 40,
  },
  pressed: {
    opacity: 0.6,
  },
}));
