import type { ReactElement } from 'react';
import { Alert, Pressable } from 'react-native';
import { Text } from 'react-native-paper';
import { signOutOfDevice } from '../push/pushSignOut';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';

/**
 * Header button that signs the photographer out after a confirmation. It sits on the band of the brand, so its text is the one
 * for that band. The navigator takes them to the login screen by itself.
 * @returns The button.
 */
export function SignOutButton(): ReactElement {
  const styles = useStyles();

  /** Asks for confirmation before ending the session. */
  function confirmSignOut(): void {
    Alert.alert('Cerrar sesión', '¿Quieres cerrar la sesión en este dispositivo?', [
      { text: 'Cancelar', style: 'cancel' },
      { text: 'Cerrar sesión', style: 'destructive', onPress: () => void signOutOfDevice() },
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

/** Styles of the button: the colors come from the theme. */
const useStyles = makeStyles((palette) => ({
  label: {
    color: palette.onBrand,
    fontFamily: FONT_FAMILIES.bodyMedium,
    fontSize: 16,
  },
  pressed: {
    opacity: 0.6,
  },
}));
