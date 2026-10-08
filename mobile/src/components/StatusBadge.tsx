import type { ReactElement } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import type { BookingStatus } from '../api/types';
import { colors, radius, spacing, type ColorToken } from '../theme/tokens';
import { BOOKING_STATUS_LABELS } from '../utils/labels';

/** Color token used for each booking status. */
export const STATUS_COLOR_TOKENS: Readonly<Record<BookingStatus, ColorToken>> = {
  Tentative: 'warning',
  Confirmed: 'success',
  Completed: 'info',
  Cancelled: 'muted',
  Expired: 'muted',
  ClientAbsent: 'danger',
};

/** Props of {@link StatusBadge}. */
export interface StatusBadgeProps {
  /** Status to display. */
  readonly status: BookingStatus;
}

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
