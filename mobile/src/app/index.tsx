import { useEffect, type ReactElement } from 'react';
import { View } from 'react-native';
import { useFollowHomeScreen } from '../hooks/useFollowHomeScreen';
import { restoreAgendaPreferences } from '../agenda/agendaManager';
import { restoreHomeScreen } from '../home/homeManager';
import { useHomeStore } from '../home/homeStore';
import { replaceWithAllBookings } from '../navigation/appNavigation';
import { useAgendaStore } from '../agenda/agendaStore';
import { MonthView } from '../components/MonthView';
import { NewBookingFab } from '../components/NewBookingFab';
import { ScheduleView } from '../components/ScheduleView';
import { TimeGridView } from '../components/TimeGridView';
import { makeStyles } from '../theme/makeStyles';
import { effectiveAgendaView } from '../utils/agenda/views';

/**
 * Home screen: the agenda of the photographer, like the calendar of the phone. It opens on today, on the view the photographer chose
 * the last time (the week the first time), and shows the bookings that still hold the schedule, or all of them when the switch
 * in the menu of the header is on. The header carries the month, the button to go back to today and the menu of views; the floating
 * button creates a booking. All the bookings, with their search and filters, are in "Todas las reservas" of the side menu.
 * @returns The screen.
 */
export default function AgendaScreen(): ReactElement {
  const styles = useStyles();
  const view = useAgendaStore((state) => effectiveAgendaView(state.view));
  const anchor = useAgendaStore((state) => state.anchor);
  const revision = useAgendaStore((state) => state.revision);
  const showAll = useAgendaStore((state) => state.showAll);
  const homeLoaded = useHomeStore((state) => state.loaded);
  useFollowHomeScreen('agenda');

  // The choices saved on the device apply as soon as they are read; until then the agenda shows the defaults.
  useEffect(() => {
    void restoreAgendaPreferences();
  }, []);

  // The app opens on the screen the photographer chose in the settings. It goes there once per run, so coming back to the agenda from
  // the menu later is not undone.
  useEffect(() => {
    if (useHomeStore.getState().loaded) {
      return;
    }

    void restoreHomeScreen().then(() => {
      const chosen = useHomeStore.getState();
      if (chosen.screen === 'bookings' && !chosen.redirected) {
        chosen.markRedirected();
        replaceWithAllBookings();
      }
    });
  }, []);

  // Until it is known which screen the app opens on, nothing is drawn, so the agenda does not flash before the list.
  if (!homeLoaded) {
    return <View style={styles.screen} />;
  }

  return (
    <View style={styles.screen}>
      {view === 'schedule' && <ScheduleView key={`${revision}-${anchor.getTime()}`} anchor={anchor} includeEnded={showAll} />}
      {(view === 'day' || view === 'threeDays' || view === 'week') && (
        <TimeGridView key={`${view}-${revision}-${anchor.getTime()}`} view={view} anchor={anchor} includeEnded={showAll} />
      )}
      {view === 'month' && <MonthView key={`${revision}-${anchor.getTime()}`} anchor={anchor} includeEnded={showAll} />}
      <NewBookingFab />
    </View>
  );
}

/** Styles of the screen. */
const useStyles = makeStyles(() => ({
  screen: {
    flex: 1,
  },
}));
