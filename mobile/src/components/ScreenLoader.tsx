import type { ReactElement } from 'react';
import { ActivityIndicator, StyleSheet, View } from 'react-native';
import { colors, spacing } from '../theme/tokens';
import type { ScreenLoaderProps } from '../types/components/ScreenLoader.types';

/**
 * Spinner centered on the whole screen, shown while the content of a screen loads.
 * @param props Component props.
 * @returns The centered spinner.
 */
export function ScreenLoader({ accessibilityLabel }: ScreenLoaderProps): ReactElement {
  return (
    <View style={styles.container}>
      <ActivityIndicator color={colors.primary} accessibilityLabel={accessibilityLabel} />
    </View>
  );
}

/** Styles of the centered spinner. */
const styles = StyleSheet.create({
  container: {
    alignItems: 'center',
    flex: 1,
    justifyContent: 'center',
    padding: spacing.lg,
  },
});
