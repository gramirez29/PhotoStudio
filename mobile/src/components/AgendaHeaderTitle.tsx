import type { ReactElement } from 'react';
import { Text } from 'react-native';
import { useAgendaStore } from '../agenda/agendaStore';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { agendaTitle } from '../utils/agenda/titles';
import { effectiveAgendaView } from '../utils/agenda/views';

/**
 * The title of the header of the agenda: the month of what the photographer is looking at, like the header of Google Calendar. In the
 * list view it follows the scroll, so it names the month of the day at the top; in the other views it names the month of the period.
 * @returns The title.
 */
export function AgendaHeaderTitle(): ReactElement {
  const styles = useStyles();
  const view = useAgendaStore((state) => effectiveAgendaView(state.view));
  const date = useAgendaStore((state) => (view === 'schedule' ? state.visibleDate : state.anchor));

  return (
    <Text accessibilityRole="header" numberOfLines={1} style={styles.title}>
      {agendaTitle(view, date, new Date())}
    </Text>
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
