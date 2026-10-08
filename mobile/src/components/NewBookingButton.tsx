import type { ReactElement } from 'react';
import { Pressable, StyleSheet, Text } from 'react-native';
import { colors, spacing } from '../theme/tokens';
import { openNewBooking } from '../utils/navigation';

/**
 * Header button that opens the create-booking form.
 * @returns The button.
 */
export function NewBookingButton(): ReactElement {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel="Nueva reserva"
      hitSlop={spacing.sm}
      onPress={openNewBooking}
      style={({ pressed }) => pressed && styles.pressed}
    >
      <Text style={styles.label}>+ Nueva</Text>
    </Pressable>
  );
}

/** Styles of the button. */
const styles = StyleSheet.create({
  label: {
    color: colors.primary,
    fontSize: 16,
    fontWeight: '700',
  },
  pressed: {
    opacity: 0.6,
  },
});
