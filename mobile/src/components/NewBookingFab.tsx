import type { ReactElement } from 'react';
import { StyleSheet } from 'react-native';
import { FAB } from 'react-native-paper';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useAppTheme } from '../hooks/useAppTheme';
import { openNewBooking } from '../navigation/appNavigation';
import { spacing } from '../theme/tokens';

/**
 * Floating button of the home screen that opens the create-booking form: the main action of the app, always within reach of the
 * thumb, in the slate blue of the logo. It sits above the system navigation bar.
 * @returns The button.
 */
export function NewBookingFab(): ReactElement {
  const insets = useSafeAreaInsets();
  const { palette } = useAppTheme();

  return (
    <FAB
      label="Nueva reserva"
      color={palette.onBrand}
      accessibilityLabel="Nueva reserva"
      onPress={openNewBooking}
      style={[styles.fab, { backgroundColor: palette.brand, bottom: insets.bottom + spacing.md }]}
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
