import { MaterialIcons } from '@expo/vector-icons';
import { useEffect, useState, type ReactElement } from 'react';
import { BackHandler, Pressable, StyleSheet, View } from 'react-native';
import { FAB } from 'react-native-paper';
import { useAppTheme } from '../hooks/useAppTheme';
import { useBottomInset } from '../hooks/useBottomInset';
import { openNewBooking } from '../navigation/appNavigation';
import { spacing } from '../theme/tokens';
import { withAlpha } from '../utils/color';

/** Diameter of the round button, the size of a Material floating button. */
const FAB_SIZE = 56;

/**
 * Floating button of the home screen: a round button with a plus that, when tapped, shows the option "Agregar reserva" above it,
 * with the rest of the screen dimmed. Tapping the option opens the create-booking form; tapping the dimmed screen, the cross
 * that the plus turns into, or the back button of the phone closes the menu without doing anything.
 * It is the main action of the app, always within reach of the thumb, in the main color of the theme (the slate blue of the
 * logo in light, copper in dark), and it sits above the system navigation bar. The menu lives inside the screen, so it never stays
 * over another screen.
 * @returns The button, and the option and the dimmed screen while it is open.
 */
export function NewBookingFab(): ReactElement {
  const { palette } = useAppTheme();
  const bottomInset = useBottomInset();
  const [open, setOpen] = useState(false);

  // The back button of the phone closes the menu before it takes the photographer out of the screen.
  useEffect(() => {
    if (!open) {
      return undefined;
    }

    const subscription = BackHandler.addEventListener('hardwareBackPress', () => {
      setOpen(false);
      return true;
    });
    return () => subscription.remove();
  }, [open]);

  return (
    <>
      {open && (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Cerrar el menú"
          onPress={() => setOpen(false)}
          style={[StyleSheet.absoluteFill, { backgroundColor: withAlpha(palette.scrim, 0.45) }]}
        />
      )}
      <View pointerEvents="box-none" style={[styles.stack, { bottom: bottomInset + spacing.md }]}>
        {open && (
          <FAB
            label="Agregar reserva"
            icon={({ size, color }) => <MaterialIcons name="add" size={size} color={color} />}
            color={palette.onPrimaryContainer}
            accessibilityLabel="Agregar reserva"
            onPress={() => {
              setOpen(false);
              openNewBooking();
            }}
            style={{ backgroundColor: palette.primaryContainer }}
          />
        )}
        <FAB
          icon={({ size, color }) => <MaterialIcons name={open ? 'close' : 'add'} size={size} color={color} />}
          color={palette.onPrimary}
          accessibilityLabel={open ? 'Cerrar el menú de reservas' : 'Nueva reserva'}
          accessibilityState={{ expanded: open }}
          onPress={() => setOpen((previous) => !previous)}
          style={[styles.round, { backgroundColor: palette.primary }]}
        />
      </View>
    </>
  );
}

/** Where the buttons sit on the screen, and the round shape; their colors come from the theme. */
const styles = StyleSheet.create({
  stack: {
    alignItems: 'flex-end',
    gap: spacing.md,
    position: 'absolute',
    right: spacing.md,
  },
  round: {
    borderRadius: FAB_SIZE / 2,
    height: FAB_SIZE,
    width: FAB_SIZE,
  },
});
