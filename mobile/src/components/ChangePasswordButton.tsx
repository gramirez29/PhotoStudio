import type { ReactElement } from 'react';
import { StyleSheet } from 'react-native';
import { Button } from 'react-native-paper';
import { openChangePassword } from '../navigation/appNavigation';

/**
 * Entry to the change-password form, as a button with a contour of the settings screen. It is a quiet entry, because it is used
 * rarely and must not compete with the main action of the screen.
 * @returns The button.
 */
export function ChangePasswordButton(): ReactElement {
  return (
    <Button mode="outlined" accessibilityLabel="Cambiar contraseña" onPress={openChangePassword} contentStyle={styles.content}>
      Cambiar contraseña
    </Button>
  );
}

/** Layout of the button; its colors come from the theme. */
const styles = StyleSheet.create({
  content: {
    minHeight: 48,
  },
});
