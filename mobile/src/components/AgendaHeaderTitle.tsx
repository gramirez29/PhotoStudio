import { useState, type ReactElement } from 'react';
import { Pressable, Text } from 'react-native';
import { useAgendaStore } from '../agenda/agendaStore';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { agendaTitle } from '../utils/agenda/titles';
import { effectiveAgendaView } from '../utils/agenda/views';
import { MiniMonthPicker } from './MiniMonthPicker';

/**
 * The title of the header of the agenda: the month of what the photographer is looking at, like the header of Google Calendar. It follows
 * the scroll of the list and the swipes of the grid, and tapping it opens a small calendar to jump to any date.
 * @returns The title.
 */
export function AgendaHeaderTitle(): ReactElement {
  const styles = useStyles();
  const view = useAgendaStore((state) => effectiveAgendaView(state.view));
  const date = useAgendaStore((state) => state.visibleDate);
  const [pickerOpen, setPickerOpen] = useState(false);

  return (
    <>
      <Pressable accessibilityRole="button" accessibilityLabel="Elegir una fecha" onPress={() => setPickerOpen(true)}>
        <Text accessibilityRole="header" numberOfLines={1} style={styles.title}>
          {agendaTitle(view, date, new Date())}
        </Text>
      </Pressable>
      <MiniMonthPicker visible={pickerOpen} onClose={() => setPickerOpen(false)} />
    </>
  );
}

/** Styles of the title: it sits on the band of the brand, so its color is the one for that band. */
const useStyles = makeStyles((palette) => ({
  title: {
    color: palette.onBrand,
    fontFamily: FONT_FAMILIES.titleBold,
    fontSize: 20,
  },
}));
