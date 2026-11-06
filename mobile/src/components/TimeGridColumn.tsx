import { useMemo, type ReactElement } from 'react';
import { View } from 'react-native';
import { HOUR_HEIGHT, NOW_LINE_INTERVAL_MS } from '../constants/agenda';
import { useAppTheme } from '../hooks/useAppTheme';
import { useNow } from '../hooks/useNow';
import { makeStyles } from '../theme/makeStyles';
import type { TimeGridColumnProps } from '../types/components/agenda.types';
import { isSameDay, minutesSinceMidnight } from '../utils/agenda/dates';
import { layoutDay } from '../utils/agenda/layout';
import { minutesToOffset } from '../utils/agenda/timeGrid';
import { TimeGridEvent } from './TimeGridEvent';

/** Size of the dot at the start of the line of the current time, in points. */
const NOW_DOT = 10;

/**
 * The column of one day of the grid: the blocks of its bookings side by side where they overlap and, when the day is today, a line
 * across the column at the current time.
 * @param props Component props.
 * @returns The column.
 */
export function TimeGridColumn({ day, bookings, width, onOpenBooking }: TimeGridColumnProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const now = useNow(NOW_LINE_INTERVAL_MS);
  const events = useMemo(() => layoutDay(bookings, day), [bookings, day]);
  const isToday = isSameDay(day, now);

  return (
    <View style={[styles.column, { width, height: HOUR_HEIGHT * 24 }]}>
      {events.map((event) => (
        <TimeGridEvent key={event.booking.id} event={event} columnWidth={width} onPress={onOpenBooking} />
      ))}
      {isToday && (
        <View
          pointerEvents="none"
          accessibilityLabel="Hora actual"
          style={[styles.nowLine, { top: minutesToOffset(minutesSinceMidnight(now)), backgroundColor: palette.error }]}
        >
          <View style={[styles.nowDot, { backgroundColor: palette.error }]} />
        </View>
      )}
    </View>
  );
}

/** Styles of the column. */
const useStyles = makeStyles((palette) => ({
  column: {
    borderLeftColor: palette.outlineVariant,
    borderLeftWidth: 1,
  },
  nowLine: {
    height: 2,
    left: 0,
    position: 'absolute',
    right: 0,
  },
  nowDot: {
    borderRadius: NOW_DOT / 2,
    height: NOW_DOT,
    left: -NOW_DOT / 2,
    position: 'absolute',
    top: -(NOW_DOT / 2) + 1,
    width: NOW_DOT,
  },
}));
