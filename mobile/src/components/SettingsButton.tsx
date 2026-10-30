import type { ReactElement } from 'react';
import { Pressable } from 'react-native';
import { Text } from 'react-native-paper';
import { openSettings } from '../navigation/appNavigation';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';

/**
 * Header button that opens the settings. It sits on the band of the brand, opposite to the one that signs out, so its text is the
 * one for that band.
 * @returns The button.
 */
export function SettingsButton(): ReactElement {
  const styles = useStyles();

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel="Ajustes"
      hitSlop={spacing.sm}
      onPress={openSettings}
      style={({ pressed }) => pressed && styles.pressed}
    >
      <Text style={styles.label} maxFontSizeMultiplier={1.2}>
        Ajustes
      </Text>
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
