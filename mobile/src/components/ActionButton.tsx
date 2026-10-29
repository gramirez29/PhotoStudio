import type { ReactElement } from 'react';
import { StyleSheet } from 'react-native';
import { Button, useTheme } from 'react-native-paper';
import type { ActionButtonProps } from '../types/components/ActionButton.types';

/**
 * Full-width button for an action on a booking: a filled Material button in the main color of the theme. Red buttons are for
 * actions that free the slot or end the booking.
 * @param props Component props.
 * @returns The button.
 */
export function ActionButton({ label, onPress, variant = 'primary', disabled = false }: ActionButtonProps): ReactElement {
  const theme = useTheme();
  const danger = variant === 'danger';

  return (
    <Button
      mode="contained"
      onPress={onPress}
      disabled={disabled}
      {...(danger ? { buttonColor: theme.colors.error, textColor: theme.colors.onError } : {})}
      style={styles.button}
      contentStyle={styles.content}
      labelStyle={styles.label}
    >
      {label}
    </Button>
  );
}

/** Layout of the button; its colors come from the theme. */
const styles = StyleSheet.create({
  button: {
    alignSelf: 'stretch',
  },
  content: {
    minHeight: 48,
  },
  label: {
    fontSize: 16,
  },
});
