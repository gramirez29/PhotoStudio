import type { ReactElement } from 'react';
import { StyleSheet, Text } from 'react-native';
import { useApiHealth } from '../hooks/useApiHealth';
import { colors, spacing } from '../theme/tokens';
import { describeApiHealth } from '../utils/apiHealth';

/**
 * Shows whether the backend is reachable and ready.
 * @returns The status line.
 */
export function ApiStatus(): ReactElement {
  const health = useApiHealth();
  const { label, color } = describeApiHealth(health.isError, health.data);

  return <Text style={[styles.status, { color: colors[color] }]}>{label}</Text>;
}

/** Styles of the status line. */
const styles = StyleSheet.create({
  status: {
    fontSize: 14,
    marginBottom: spacing.md,
  },
});
