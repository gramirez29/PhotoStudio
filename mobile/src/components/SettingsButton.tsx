import { MaterialIcons } from '@expo/vector-icons';
import type { ReactElement } from 'react';
import { Pressable } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import { openSettings } from '../navigation/appNavigation';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';

/** Size of the gear, in points. */
const GEAR_SIZE = 26;

/**
 * Header button that opens the settings: a gear. It sits on the band of the brand, opposite to the one that signs out, so its
 * color is the one for that band. The gear is a drawing: the name of the button is what the screen reader reads.
 * @returns The button.
 */
export function SettingsButton(): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel="Ajustes"
      hitSlop={spacing.md}
      onPress={openSettings}
      style={({ pressed }) => [styles.button, pressed && styles.pressed]}
    >
      <MaterialIcons name="settings" size={GEAR_SIZE} color={palette.onBrand} accessible={false} importantForAccessibility="no" />
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
