import type { ReactElement } from 'react';
import { Pressable, StyleSheet, Text } from 'react-native';
import { colors, radius, spacing } from '../theme/tokens';
import type { ActionButtonProps } from '../types/components/ActionButton.types';

/**
 * Full-width button for an action on a booking. Red buttons are for actions that free the slot or end the booking.
 * @param props Component props.
 * @returns The button.
 */
export function ActionButton({ label, onPress, variant = 'primary', disabled = false }: ActionButtonProps): ReactElement {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ disabled }}
      disabled={disabled}
      onPress={onPress}
      style={({ pressed }) => [
        styles.button,
        variant === 'danger' && styles.danger,
        disabled && styles.disabled,
        pressed && styles.pressed,
      ]}
    >
      <Text style={styles.label}>{label}</Text>
    </Pressable>
  );
}

/** Styles of the button. */
const styles = StyleSheet.create({
  button: {
    alignItems: 'center',
    backgroundColor: colors.primary,
    borderRadius: radius.sm,
    paddingVertical: spacing.md,
  },
  danger: {
    backgroundColor: colors.danger,
  },
  disabled: {
    opacity: 0.5,
  },
  pressed: {
    opacity: 0.8,
  },
  label: {
    color: colors.textInverse,
    fontSize: 16,
    fontWeight: '600',
  },
});
