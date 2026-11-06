import { useRef, type ReactElement } from 'react';
import { ScrollView, Text, View, type NativeScrollEvent, type NativeSyntheticEvent } from 'react-native';
import { HOUR_GUTTER_WIDTH, HOUR_HEIGHT } from '../constants/agenda';
import { useBottomInset } from '../hooks/useBottomInset';
import { useToday } from '../hooks/useToday';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';
import type { TimeGridPageProps } from '../types/components/agenda.types';
import { isSameDay } from '../utils/agenda/dates';
import { weekdayShort } from '../utils/agenda/labels';
import { hourLabel } from '../utils/agenda/timeGrid';
import { TimeGridColumn } from './TimeGridColumn';

/** Room left under the last hour so the floating button never hides the end of the day. */
const GRID_END_SPACE = 96;

/** The 24 hours of a day. */
const HOURS = Array.from({ length: 24 }, (_, hour) => hour);

/**
 * One page of the grid of hours: the names of its days on top and, under them, the hours of the day from midnight to midnight with a
 * column per day. Every hour has a line and every half hour a lighter one, so a session of half an hour fills one row. The hours
 * scroll up and down on their own; the names of the days stay in place.
 * @param props Component props.
 * @returns The page.
 */
export function TimeGridPage({ days, bookings, width, getOffset, onScrollOffset, onOpenBooking }: TimeGridPageProps): ReactElement {
  const styles = useStyles();
  const bottomInset = useBottomInset();
  const today = useToday();
  const scroller = useRef<ScrollView>(null);
  const columnWidth = (width - HOUR_GUTTER_WIDTH) / days.length;

  return (
    <View style={[styles.page, { width }]}>
      <View style={styles.header}>
        <View style={{ width: HOUR_GUTTER_WIDTH }} />
        {days.map((day) => {
          const isToday = isSameDay(day, today);
          return (
            <View key={day.getTime()} style={[styles.dayName, { width: columnWidth }]} accessibilityRole="header">
              <Text style={[styles.weekday, isToday && styles.todayText]}>{weekdayShort(day)}</Text>
              <View style={[styles.dayNumberBox, isToday && styles.todayBox]}>
                <Text style={[styles.dayNumber, isToday && styles.todayBoxText]}>{day.getDate()}</Text>
              </View>
            </View>
          );
        })}
      </View>
      <ScrollView
        ref={scroller}
        accessibilityLabel="Horas del día"
        scrollEventThrottle={32}
        onLayout={() => scroller.current?.scrollTo({ y: getOffset(), animated: false })}
        onScroll={(event: NativeSyntheticEvent<NativeScrollEvent>) => onScrollOffset(event.nativeEvent.contentOffset.y)}
        contentContainerStyle={{ paddingBottom: GRID_END_SPACE + bottomInset }}
      >
        <View style={styles.body}>
          <View style={{ width: HOUR_GUTTER_WIDTH }}>
            {HOURS.map((hour) => (
              <View key={hour} style={styles.hourLabelBox}>
                <Text style={styles.hourLabel}>{hourLabel(hour)}</Text>
              </View>
            ))}
          </View>
          <View style={styles.columns}>
            <View style={styles.lines} pointerEvents="none">
              {HOURS.map((hour) => (
                <View key={hour} style={styles.hourRow}>
                  <View style={styles.halfHourLine} />
                </View>
              ))}
            </View>
            <View style={styles.row}>
              {days.map((day) => (
                <TimeGridColumn key={day.getTime()} day={day} bookings={bookings} width={columnWidth} onOpenBooking={onOpenBooking} />
              ))}
            </View>
          </View>
        </View>
      </ScrollView>
    </View>
  );
}

/** Styles of the page. */
const useStyles = makeStyles((palette) => ({
  page: {
    flex: 1,
  },
  header: {
    borderBottomColor: palette.outlineVariant,
    borderBottomWidth: 1,
    flexDirection: 'row',
    paddingVertical: spacing.xs,
  },
  dayName: {
    alignItems: 'center',
    gap: 2,
  },
  weekday: {
    color: palette.onSurfaceVariant,
    fontFamily: FONT_FAMILIES.bodyMedium,
    fontSize: 12,
  },
  dayNumberBox: {
    alignItems: 'center',
    borderRadius: 16,
    height: 32,
    justifyContent: 'center',
    minWidth: 32,
  },
  dayNumber: {
    color: palette.onSurface,
    fontFamily: FONT_FAMILIES.titleBold,
    fontSize: 16,
  },
  todayText: {
    color: palette.primary,
  },
  todayBox: {
    backgroundColor: palette.primary,
  },
  todayBoxText: {
    color: palette.onPrimary,
  },
  body: {
    flexDirection: 'row',
  },
  hourLabelBox: {
    height: HOUR_HEIGHT,
    paddingRight: spacing.xs,
  },
  hourLabel: {
    color: palette.onSurfaceVariant,
    fontFamily: FONT_FAMILIES.body,
    fontSize: 11,
    marginTop: -7,
    textAlign: 'right',
  },
  columns: {
    flex: 1,
  },
  lines: {
    borderBottomColor: palette.outlineVariant,
    borderBottomWidth: 1,
    position: 'absolute',
    left: 0,
    right: 0,
    top: 0,
  },
  hourRow: {
    borderTopColor: palette.outlineVariant,
    borderTopWidth: 1,
    height: HOUR_HEIGHT,
  },
  halfHourLine: {
    borderTopColor: palette.outlineVariant,
    borderTopWidth: 1,
    marginTop: HOUR_HEIGHT / 2 - 1,
    opacity: 0.45,
  },
  row: {
    flexDirection: 'row',
  },
}));
