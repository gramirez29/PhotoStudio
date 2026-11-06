import { MaterialIcons } from '@expo/vector-icons';
import { useMemo, useState, type ReactElement } from 'react';
import { Modal, Pressable, Text, View } from 'react-native';
import { useAgendaStore } from '../agenda/agendaStore';
import { useAppTheme } from '../hooks/useAppTheme';
import { useTopInset } from '../hooks/useTopInset';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { radius, spacing } from '../theme/tokens';
import type { MiniMonthPickerProps } from '../types/components/agenda.types';
import { startOfMonth } from '../utils/agenda/dates';
import { weekdayShort } from '../utils/agenda/labels';
import { buildMonthGrid } from '../utils/agenda/monthGrid';
import { shiftAnchor } from '../utils/agenda/ranges';
import { agendaTitle, monthName } from '../utils/agenda/titles';
import { withAlpha } from '../utils/color';

/** Height of the bar of the header, so the picker opens right under it. */
const HEADER_HEIGHT = 56;

/** How dark the rest of the screen gets while the picker is open. */
const DIMMING = 0.4;

/** Size of the arrows, in points. */
const ICON_SIZE = 24;

/**
 * The small calendar that opens from the title of the agenda to jump to any date: it shows one month with arrows to go to the
 * previous or the next, today marked, and the day on screen filled. Choosing a day moves the agenda there, in the view that is open.
 * @param props Component props.
 * @returns The picker, which draws nothing while it is closed.
 */
export function MiniMonthPicker({ visible, onClose }: MiniMonthPickerProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const topInset = useTopInset();
  const visibleDate = useAgendaStore((state) => state.visibleDate);
  const setAnchor = useAgendaStore((state) => state.setAnchor);
  const [shown, setShown] = useState(() => startOfMonth(visibleDate));
  const weeks = useMemo(() => buildMonthGrid(shown, [], new Date()), [shown]);

  const choose = (day: Date): void => {
    setAnchor(day);
    onClose();
  };

  return (
    <Modal visible={visible} transparent animationType="fade" statusBarTranslucent onRequestClose={onClose} onShow={() => setShown(startOfMonth(visibleDate))}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Cerrar el calendario"
        onPress={onClose}
        style={[styles.backdrop, { backgroundColor: withAlpha(palette.scrim, DIMMING), paddingTop: topInset + HEADER_HEIGHT }]}
      >
        {/* A tap on the card must not reach the backdrop behind it, which would close the picker. */}
        <Pressable accessible={false} style={styles.card}>
          <View style={styles.bar}>
            <Pressable accessibilityRole="button" accessibilityLabel="Mes anterior" hitSlop={spacing.sm} onPress={() => setShown((month) => shiftAnchor('month', month, -1))}>
              <MaterialIcons name="chevron-left" size={ICON_SIZE} color={palette.onSurface} accessible={false} importantForAccessibility="no" />
            </Pressable>
            <Text accessibilityRole="header" style={styles.month}>
              {agendaTitle('month', shown, new Date())}
            </Text>
            <Pressable accessibilityRole="button" accessibilityLabel="Mes siguiente" hitSlop={spacing.sm} onPress={() => setShown((month) => shiftAnchor('month', month, 1))}>
              <MaterialIcons name="chevron-right" size={ICON_SIZE} color={palette.onSurface} accessible={false} importantForAccessibility="no" />
            </Pressable>
          </View>
          <View style={styles.week}>
            {(weeks[0] ?? []).map((cell) => (
              <Text key={cell.key} style={styles.weekday}>
                {weekdayShort(cell.date).charAt(0).toUpperCase()}
              </Text>
            ))}
          </View>
          {weeks.map((week) => (
            <View key={week[0]?.key} style={styles.week}>
              {week.map((cell) => {
                const selected = cell.date.getTime() === visibleDate.getTime();
                return (
                  <Pressable
                    key={cell.key}
                    accessibilityRole="button"
                    accessibilityLabel={`${cell.date.getDate()} de ${monthName(cell.date).toLowerCase()}${cell.isToday ? ', hoy' : ''}`}
                    accessibilityState={{ selected }}
                    onPress={() => choose(cell.date)}
                    style={styles.dayBox}
                  >
                    <View style={[styles.day, cell.isToday && styles.today, selected && styles.selected]}>
                      <Text style={[styles.dayText, !cell.inMonth && styles.outside, selected && styles.selectedText]}>{cell.date.getDate()}</Text>
                    </View>
                  </Pressable>
                );
              })}
            </View>
          ))}
        </Pressable>
      </Pressable>
    </Modal>
  );
}

/** Styles of the picker: the colors come from the theme. */
const useStyles = makeStyles((palette) => ({
  backdrop: {
    alignItems: 'center',
    flex: 1,
    paddingHorizontal: spacing.sm,
  },
  card: {
    backgroundColor: palette.surface,
    borderColor: palette.outlineVariant,
    borderRadius: radius.md,
    borderWidth: 1,
    padding: spacing.sm,
    width: 320,
  },
  bar: {
    alignItems: 'center',
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingBottom: spacing.xs,
  },
  month: {
    color: palette.onSurface,
    fontFamily: FONT_FAMILIES.titleBold,
    fontSize: 16,
  },
  week: {
    flexDirection: 'row',
  },
  weekday: {
    color: palette.onSurfaceVariant,
    flex: 1,
    fontFamily: FONT_FAMILIES.bodyMedium,
    fontSize: 12,
    paddingVertical: spacing.xs,
    textAlign: 'center',
  },
  dayBox: {
    alignItems: 'center',
    flex: 1,
    height: 40,
    justifyContent: 'center',
  },
  day: {
    alignItems: 'center',
    borderColor: 'transparent',
    borderRadius: 18,
    borderWidth: 1,
    height: 36,
    justifyContent: 'center',
    width: 36,
  },
  today: {
    borderColor: palette.primary,
  },
  selected: {
    backgroundColor: palette.primary,
  },
  dayText: {
    color: palette.onSurface,
    fontFamily: FONT_FAMILIES.body,
    fontSize: 14,
  },
  outside: {
    opacity: 0.4,
  },
  selectedText: {
    color: palette.onPrimary,
  },
}));
