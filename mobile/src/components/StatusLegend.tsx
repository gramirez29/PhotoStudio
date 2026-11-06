import type { ReactElement } from 'react';
import { Text, View } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';
import type { StatusLegendProps } from '../types/components/home.types';
import { formatPercent } from '../utils/home/donut';
import { BOOKING_STATUS_LABELS } from '../utils/labels';
import { STATUS_COLOR_ROLES } from '../utils/statusColors';

/**
 * The legend of the donut chart: one row for each status that has bookings, with a dot of its color, its name, how many bookings it
 * has and what share of the total that is. The color is never the only thing that says the status: the name is written.
 * @param props Component props.
 * @returns The legend, which is empty while there are no bookings.
 */
export function StatusLegend({ stats }: StatusLegendProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();

  return (
    <View style={styles.legend}>
      {stats.byStatus
        .filter((entry) => entry.count > 0)
        .map((entry) => (
          <View
            key={entry.status}
            accessible
            accessibilityLabel={`${BOOKING_STATUS_LABELS[entry.status]}: ${entry.count}, ${formatPercent(entry.count / stats.total)}`}
            style={styles.row}
          >
            <View style={[styles.dot, { backgroundColor: palette[STATUS_COLOR_ROLES[entry.status]] }]} />
            <Text style={styles.label}>{BOOKING_STATUS_LABELS[entry.status]}</Text>
            <Text style={styles.count}>{entry.count}</Text>
            <Text style={styles.percent}>{formatPercent(entry.count / stats.total)}</Text>
          </View>
        ))}
    </View>
  );
}

/** Styles of the legend: the colors come from the theme. */
const useStyles = makeStyles((palette) => ({
  legend: {
    gap: spacing.xs,
  },
  row: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: spacing.sm,
    minHeight: 32,
  },
  dot: {
    borderRadius: 7,
    height: 14,
    width: 14,
  },
  label: {
    color: palette.onSurface,
    flex: 1,
    fontFamily: FONT_FAMILIES.body,
    fontSize: 16,
  },
  count: {
    color: palette.onSurface,
    fontFamily: FONT_FAMILIES.bodyBold,
    fontSize: 16,
  },
  percent: {
    color: palette.onSurfaceVariant,
    fontFamily: FONT_FAMILIES.body,
    fontSize: 14,
    minWidth: 48,
    textAlign: 'right',
  },
}));
