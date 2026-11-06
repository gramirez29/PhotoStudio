import { useMemo, type ReactElement } from 'react';
import { Text, View } from 'react-native';
import { MONTH_CELL_MAX_HEIGHT } from '../constants/agenda';
import { useToday } from '../hooks/useToday';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import type { MonthPageProps } from '../types/components/agenda.types';
import { addDays, startOfWeek } from '../utils/agenda/dates';
import { weekdayShort } from '../utils/agenda/labels';
import { buildMonthGrid } from '../utils/agenda/monthGrid';
import { MonthDayCell } from './MonthDayCell';

/** Height of the row with the names of the weekdays. */
const WEEKDAY_ROW_HEIGHT = 28;

/**
 * One page of the month view: the names of the weekdays and the weeks of the month, from Sunday, each week taking the same share of
 * the height. The days of the neighbor months that fill the first and the last week are dimmed.
 * @param props Component props.
 * @returns The page.
 */
export function MonthPage({ month, bookings, width, height, onOpenDay }: MonthPageProps): ReactElement {
  const styles = useStyles();
  const today = useToday();
  const weeks = useMemo(() => buildMonthGrid(month, bookings, today), [month, bookings, today]);
  const cellWidth = width / 7;
  const cellHeight = Math.min(MONTH_CELL_MAX_HEIGHT, Math.max(0, (height - WEEKDAY_ROW_HEIGHT) / weeks.length));
  const firstWeek = startOfWeek(month);

  return (
    <View style={{ width, height }}>
      <View style={styles.weekdays}>
        {Array.from({ length: 7 }, (_, index) => (
          <Text key={index} style={[styles.weekday, { width: cellWidth }]}>
            {weekdayShort(addDays(firstWeek, index))}
          </Text>
        ))}
      </View>
      {weeks.map((week) => (
        <View key={week[0]?.key} style={styles.week}>
          {week.map((cell) => (
            <MonthDayCell key={cell.key} cell={cell} width={cellWidth} height={cellHeight} onPress={onOpenDay} />
          ))}
        </View>
      ))}
    </View>
  );
}

/** Styles of the page. */
const useStyles = makeStyles((palette) => ({
  weekdays: {
    flexDirection: 'row',
    height: WEEKDAY_ROW_HEIGHT,
  },
  weekday: {
    color: palette.onSurfaceVariant,
    fontFamily: FONT_FAMILIES.bodyMedium,
    fontSize: 12,
    paddingTop: 6,
    textAlign: 'center',
  },
  week: {
    flexDirection: 'row',
  },
}));
