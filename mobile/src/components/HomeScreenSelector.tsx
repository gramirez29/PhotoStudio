import type { ReactElement } from 'react';
import { StyleSheet, View } from 'react-native';
import { SegmentedButtons, Text, useTheme } from 'react-native-paper';
import { HOME_SCREENS } from '../constants/home';
import { changeHomeScreen } from '../home/homeManager';
import { useHomeStore } from '../home/homeStore';
import { spacing } from '../theme/tokens';
import { HOME_SCREEN_LABELS, isHomeScreen } from '../utils/home';

/**
 * The selector of the screen the app opens on: the agenda or the list of all the bookings. The choice is remembered and applies the
 * next time the app opens. It lives in the settings screen.
 * @returns The selector with its title.
 */
export function HomeScreenSelector(): ReactElement {
  const theme = useTheme();
  const screen = useHomeStore((state) => state.screen);

  return (
    <View style={styles.container}>
      <Text variant="labelLarge" style={{ color: theme.colors.onSurfaceVariant }}>
        Pantalla principal
      </Text>
      <SegmentedButtons
        value={screen}
        onValueChange={(selected) => {
          if (isHomeScreen(selected)) {
            void changeHomeScreen(selected);
          }
        }}
        buttons={HOME_SCREENS.map((option) => ({
          value: option,
          label: HOME_SCREEN_LABELS[option],
          accessibilityLabel: `Pantalla principal: ${HOME_SCREEN_LABELS[option]}`,
        }))}
      />
    </View>
  );
}

/** Layout of the selector; its colors come from the theme. */
const styles = StyleSheet.create({
  container: {
    gap: spacing.xs,
  },
});
