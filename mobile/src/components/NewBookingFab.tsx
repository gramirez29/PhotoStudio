import type { ReactElement } from 'react';
import { StyleSheet } from 'react-native';
import { FAB } from 'react-native-paper';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { openNewBooking } from '../navigation/appNavigation';
import { spacing } from '../theme/tokens';

/**
 * Floating button of the home screen that opens the create-booking form: the main action of the app, always within reach of the
 * thumb. It sits above the system navigation bar.
 * @returns The button.
 */
export function NewBookingFab(): ReactElement {
  const insets = useSafeAreaInsets();

  return (
    <FAB
      variant="primary"
      label="Nueva reserva"
      accessibilityLabel="Nueva reserva"
      onPress={openNewBooking}
      style={[styles.fab, { bottom: insets.bottom + spacing.md }]}
    />
  );
}

/** Where the button sits on the screen; its colors come from the theme. */
const styles = StyleSheet.create({
  fab: {
    position: 'absolute',
    right: spacing.md,
  },
});
