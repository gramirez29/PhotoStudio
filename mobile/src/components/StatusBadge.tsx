import type { ReactElement } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { colors, radius, spacing } from '../theme/tokens';
import type { StatusBadgeProps } from '../types/components/StatusBadge.types';
import { BOOKING_STATUS_LABELS } from '../utils/labels';
import { STATUS_COLOR_TOKENS } from '../utils/statusColors';

/**
 * Pill that shows the booking status with its color.
 * @param props Component props.
 * @returns The badge.
 */
export function StatusBadge({ status }: StatusBadgeProps): ReactElement {
  const backgroundColor = colors[STATUS_COLOR_TOKENS[status]];

  return (
    <View
      accessibilityRole="text"
      accessibilityLabel={`Estado: ${BOOKING_STATUS_LABELS[status]}`}
      style={[styles.badge, { backgroundColor }]}
    >
      <Text style={styles.label}>{BOOKING_STATUS_LABELS[status]}</Text>
    </View>
  );
}

/** Styles of the badge. */
const styles = StyleSheet.create({
  badge: {
    alignSelf: 'flex-start',
    borderRadius: radius.pill,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.xs,
  },
  label: {
    color: colors.textInverse,
    fontSize: 13,
    fontWeight: '600',
  },
});
