import type { ReactElement } from 'react';
import { ScrollView } from 'react-native';
import { BookingStatsCard } from '../components/BookingStatsCard';
import { HomePendingCard } from '../components/HomePendingCard';
import { HomeShortcutsCard } from '../components/HomeShortcutsCard';
import { useBottomInset } from '../hooks/useBottomInset';
import { useExitOnDoubleBack } from '../hooks/useExitOnDoubleBack';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';

/**
 * Home screen, the first of the stack: the bookings counted by status in a donut chart for the period the photographer picks, what is
 * waiting (notices and refunds) and the ways to go on (the agenda, all the bookings, a new booking and the web area of the
 * photographer). Every other screen opens on top of it and goes back to it; going back from here asks once before leaving the app.
 * @returns The screen.
 */
export default function HomeScreen(): ReactElement {
  const styles = useStyles();
  const bottomInset = useBottomInset();
  useExitOnDoubleBack();

  return (
    <ScrollView contentContainerStyle={[styles.container, { paddingBottom: spacing.lg + bottomInset }]}>
      <BookingStatsCard />
      <HomePendingCard />
      <HomeShortcutsCard />
    </ScrollView>
  );
}

/** Layout of the screen. */
const useStyles = makeStyles(() => ({
  container: {
    gap: spacing.md,
    padding: spacing.md,
  },
}));
