import type { ReactElement } from 'react';
import { StyleSheet, View } from 'react-native';
import { spacing } from '../theme/tokens';

/**
 * Vertical gap between two cards of a list.
 * @returns The spacer.
 */
export function ListSeparator(): ReactElement {
  return <View style={styles.separator} />;
}

/** Styles of the spacer. */
const styles = StyleSheet.create({
  separator: {
    height: spacing.sm,
  },
});
