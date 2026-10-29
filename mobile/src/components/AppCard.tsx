import type { ReactElement } from 'react';
import { StyleSheet } from 'react-native';
import { Surface, useTheme } from 'react-native-paper';
import { spacing } from '../theme/tokens';
import type { AppCardProps } from '../types/components/AppCard.types';

/**
 * Tonal Material card: the block that groups what belongs together on a screen (amounts, the contract, the actions). It takes its
 * color from the theme, so it follows the light and dark themes.
 * @param props Component props.
 * @returns The card.
 */
export function AppCard({ children, style }: AppCardProps): ReactElement {
  const theme = useTheme();

  return (
    <Surface elevation={0} style={[styles.card, { backgroundColor: theme.colors.surfaceVariant }, style]}>
      {children}
    </Surface>
  );
}

/** Corners and padding of the card. */
const styles = StyleSheet.create({
  card: {
    borderRadius: 16,
    padding: spacing.md,
  },
});
