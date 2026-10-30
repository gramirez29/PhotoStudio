import type { ReactElement } from 'react';
import { StyleSheet, View } from 'react-native';
import { SegmentedButtons, Text, useTheme } from 'react-native-paper';
import { THEME_PREFERENCES } from '../constants/appearance';
import { useThemePreference } from '../hooks/useThemePreference';
import { spacing } from '../theme/tokens';
import { isThemePreference, themePreferenceLabel } from '../utils/appearance';

/**
 * The selector of the appearance: follow the phone, always light or always dark. A choice applies right away, to every screen,
 * and is remembered.
 * @returns The selector with its title.
 */
export function ThemeSelector(): ReactElement {
  const theme = useTheme();
  const { preference, choose } = useThemePreference();

  return (
    <View style={styles.container}>
      <Text variant="labelLarge" style={{ color: theme.colors.onSurfaceVariant }}>
        Apariencia
      </Text>
      <SegmentedButtons
        value={preference}
        onValueChange={(selected) => {
          if (isThemePreference(selected)) {
            choose(selected);
          }
        }}
        buttons={THEME_PREFERENCES.map((option) => ({
          value: option,
          label: themePreferenceLabel(option),
          accessibilityLabel: `Apariencia: ${themePreferenceLabel(option)}`,
        }))}
      />
    </View>
  );
}

/** Layout of the selector; its colors come from the theme. */
const styles = StyleSheet.create({
  container: {
    gap: spacing.xs,
    marginBottom: spacing.md,
  },
});
