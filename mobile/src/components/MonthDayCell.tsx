import type { ReactElement } from 'react';
import { Pressable, Text, View } from 'react-native';
import { MONTH_CELL_HEADER, MONTH_CHIP_HEIGHT } from '../constants/agenda';
import { useAppTheme } from '../hooks/useAppTheme';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import type { MonthDayCellProps } from '../types/components/agenda.types';
import { formatDate } from '../utils/format';
import { STATUS_COLOR_ROLES } from '../utils/statusColors';

/**
 * A day of the grid of the month: its number (inside a filled circle when it is today, dimmed when it belongs to another month) and
 * its bookings as thin bars of the color of their status with the name of the client, as many as fit and then "+n" for the rest.
 * Tapping the cell opens that day.
 * @param props Component props.
 * @returns The cell.
 */
export function MonthDayCell({ cell, width, height, onPress }: MonthDayCellProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const room = Math.max(0, Math.floor((height - MONTH_CELL_HEADER) / MONTH_CHIP_HEIGHT));
  const total = cell.bookings.length;
  const hidden = total > room ? total - Math.max(room - 1, 0) : 0;
  const shown = hidden > 0 ? cell.bookings.slice(0, Math.max(room - 1, 0)) : cell.bookings;
  const count = total === 0 ? 'sin reservas' : total === 1 ? '1 reserva' : `${total} reservas`;

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`${formatDate(cell.date)}${cell.isToday ? ', hoy' : ''}, ${count}`}
      onPress={() => onPress(cell.date)}
      style={[styles.cell, { width, height }]}
    >
      <View style={[styles.number, cell.isToday && styles.todayNumber]}>
        <Text style={[styles.numberText, !cell.inMonth && styles.outside, cell.isToday && styles.todayNumberText]}>{cell.date.getDate()}</Text>
      </View>
      {shown.map((booking) => (
        <View key={booking.id} style={[styles.chip, { backgroundColor: palette[STATUS_COLOR_ROLES[booking.status]] }, !cell.inMonth && styles.outside]}>
          <Text numberOfLines={1} style={styles.chipText}>
            {booking.clientName}
          </Text>
        </View>
      ))}
      {hidden > 0 && <Text style={styles.more}>+{hidden}</Text>}
    </Pressable>
  );
}

/** Styles of the cell. */
const useStyles = makeStyles((palette) => ({
  cell: {
    borderColor: palette.outlineVariant,
    borderLeftWidth: 1,
    borderTopWidth: 1,
    overflow: 'hidden',
    paddingHorizontal: 1,
  },
  number: {
    alignItems: 'center',
    alignSelf: 'center',
    borderRadius: 11,
    height: 22,
    justifyContent: 'center',
    marginVertical: 2,
    minWidth: 22,
  },
  todayNumber: {
    backgroundColor: palette.primary,
  },
  numberText: {
    color: palette.onSurface,
    fontFamily: FONT_FAMILIES.bodyMedium,
    fontSize: 12,
  },
  todayNumberText: {
    color: palette.onPrimary,
  },
  outside: {
    opacity: 0.45,
  },
  chip: {
    borderRadius: 3,
    height: MONTH_CHIP_HEIGHT - 2,
    justifyContent: 'center',
    marginBottom: 2,
    paddingHorizontal: 3,
  },
  chipText: {
    color: palette.onError,
    fontFamily: FONT_FAMILIES.bodyMedium,
    fontSize: 10,
  },
  more: {
    color: palette.onSurfaceVariant,
    fontFamily: FONT_FAMILIES.bodyMedium,
    fontSize: 10,
    paddingHorizontal: 3,
  },
}));
