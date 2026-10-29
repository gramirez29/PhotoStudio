import type { ReactElement } from 'react';
import { StyleSheet, View } from 'react-native';
import { ActivityIndicator } from 'react-native-paper';
import { spacing } from '../theme/tokens';
import type { ScreenLoaderProps } from '../types/components/ScreenLoader.types';

/**
 * Spinner centered on the whole screen, shown while the content of a screen loads.
 * @param props Component props.
 * @returns The centered spinner.
 */
export function ScreenLoader({ accessibilityLabel }: ScreenLoaderProps): ReactElement {
  return (
    <View style={styles.container}>
      <ActivityIndicator animating size="large" accessibilityLabel={accessibilityLabel} />
    </View>
  );
}

/** Layout of the centered spinner; its color comes from the theme. */
const styles = StyleSheet.create({
  container: {
    alignItems: 'center',
    flex: 1,
    justifyContent: 'center',
    padding: spacing.lg,
  },
});
