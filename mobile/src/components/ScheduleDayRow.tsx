import type { ReactElement } from 'react';
import { Text, View } from 'react-native';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { radius, spacing } from '../theme/tokens';
import type { ScheduleDayRowProps } from '../types/components/agenda.types';
import { weekdayShort } from '../utils/agenda/labels';
import { formatDate } from '../utils/format';
import { AgendaEventChip } from './AgendaEventChip';

/** Diameter of the circle of the day number, which is filled for today. */
const NUMBER_SIZE = 36;

/** Width of the column of the day, so the bookings of every day start at the same place. */
const DAY_COLUMN_WIDTH = 56;

/**
 * A day of the list view: at the left, the weekday and the day number (inside a filled circle when it is today) and, at the right,
 * its bookings one under the other. Today shows a quiet line when it has none, so the list always says where the present is.
 * @param props Component props.
 * @returns The row.
 */
export function ScheduleDayRow({ section, onOpenBooking }: ScheduleDayRowProps): ReactElement {
  const styles = useStyles();

  return (
    <View style={styles.row}>
      <View
        accessible
        accessibilityRole="header"
        accessibilityLabel={`${formatDate(section.date)}${section.isToday ? ', hoy' : ''}`}
        style={styles.day}
      >
        <Text style={[styles.weekday, section.isToday && styles.todayText]}>{weekdayShort(section.date)}</Text>
        <View style={[styles.number, section.isToday && styles.todayNumber]}>
          <Text style={[styles.numberText, section.isToday && styles.todayNumberText]}>{section.date.getDate()}</Text>
        </View>
      </View>
      <View style={styles.bookings}>
        {section.bookings.length === 0 ? (
          <Text style={styles.empty}>Sin reservas</Text>
        ) : (
          section.bookings.map((booking) => <AgendaEventChip key={booking.id} booking={booking} onPress={onOpenBooking} />)
        )}
      </View>
    </View>
  );
}

/** Styles of the row: the colors come from the theme. */
const useStyles = makeStyles((palette) => ({
  row: {
    flexDirection: 'row',
    gap: spacing.sm,
    paddingVertical: spacing.sm,
  },
  day: {
    alignItems: 'center',
    width: DAY_COLUMN_WIDTH,
  },
  weekday: {
    color: palette.onSurfaceVariant,
    fontFamily: FONT_FAMILIES.bodyMedium,
    fontSize: 12,
    textTransform: 'uppercase',
  },
  todayText: {
    color: palette.primary,
  },
  number: {
    alignItems: 'center',
    borderRadius: radius.pill,
    height: NUMBER_SIZE,
    justifyContent: 'center',
    width: NUMBER_SIZE,
  },
  todayNumber: {
    backgroundColor: palette.primary,
  },
  numberText: {
    color: palette.onSurface,
    fontFamily: FONT_FAMILIES.titleBold,
    fontSize: 18,
  },
  todayNumberText: {
    color: palette.onPrimary,
  },
  bookings: {
    flex: 1,
    gap: spacing.xs,
    justifyContent: 'center',
    minHeight: NUMBER_SIZE,
  },
  empty: {
    color: palette.onSurfaceVariant,
    fontFamily: FONT_FAMILIES.body,
    fontSize: 14,
  },
}));
