import type { ReactElement } from 'react';
import { StyleSheet } from 'react-native';
import { Button, useTheme } from 'react-native-paper';
import type { ActionButtonProps } from '../types/components/ActionButton.types';

/**
 * Full-width button for an action on a booking: a filled Material button in the main color of the theme. The actions that free
 * the slot or end the booking are outlined in red, so they can never be mistaken for the main action, whatever the theme.
 * @param props Component props.
 * @returns The button.
 */
export function ActionButton({ label, onPress, variant = 'primary', disabled = false }: ActionButtonProps): ReactElement {
  const theme = useTheme();
  const danger = variant === 'danger';

  return (
    <Button
      mode={danger ? 'outlined' : 'contained'}
      onPress={onPress}
      disabled={disabled}
      {...(danger ? { textColor: theme.colors.error } : {})}
      style={[styles.button, danger && !disabled && { borderColor: theme.colors.error }]}
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
