import type { ReactElement } from 'react';
import { StyleSheet, View } from 'react-native';
import { Text, useTheme } from 'react-native-paper';
import { useAppTheme } from '../hooks/useAppTheme';
import { radius, spacing } from '../theme/tokens';
import type { StatusBadgeProps } from '../types/components/StatusBadge.types';
import { BOOKING_STATUS_LABELS } from '../utils/labels';
import { STATUS_COLOR_ROLES } from '../utils/statusColors';

/**
 * Pill that shows the booking status: a dot in the color of the status next to its name, on a tonal background. The color is
 * never the only carrier of the meaning: the name is always written.
 * @param props Component props.
 * @returns The badge.
 */
export function StatusBadge({ status }: StatusBadgeProps): ReactElement {
  const theme = useTheme();
  const { palette } = useAppTheme();

  return (
    <View
      accessibilityRole="text"
      accessibilityLabel={`Estado: ${BOOKING_STATUS_LABELS[status]}`}
      style={[styles.badge, { backgroundColor: theme.colors.surfaceVariant }]}
    >
      <View style={[styles.dot, { backgroundColor: palette[STATUS_COLOR_ROLES[status]] }]} />
      <Text variant="labelLarge" style={{ color: theme.colors.onSurfaceVariant }}>
        {BOOKING_STATUS_LABELS[status]}
      </Text>
    </View>
  );
}

/** Layout of the badge; its colors come from the theme. */
const styles = StyleSheet.create({
  badge: {
    alignItems: 'center',
    alignSelf: 'flex-start',
    borderRadius: radius.pill,
    flexDirection: 'row',
    gap: spacing.sm,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.xs,
  },
  dot: {
    borderRadius: radius.pill,
    height: 10,
    width: 10,
  },
});
