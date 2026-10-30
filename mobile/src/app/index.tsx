import type { ReactElement } from 'react';
import { ActivityIndicator, FlatList, Text, View } from 'react-native';
import { BookingListItem } from '../components/BookingListItem';
import { ListSeparator } from '../components/ListSeparator';
import { MaintenanceButton } from '../components/MaintenanceButton';
import { NewBookingFab } from '../components/NewBookingFab';
import { NotificationsButton } from '../components/NotificationsButton';
import { Placeholder } from '../components/Placeholder';
import { RefundsButton } from '../components/RefundsButton';
import { useBookings } from '../hooks/useBookings';
import { openBooking } from '../navigation/appNavigation';
import { useSessionStore } from '../session/sessionStore';
import { spacing } from '../theme/tokens';
import type { BookingSummaryResponse } from '../types/api/booking';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { useAppTheme } from '../hooks/useAppTheme';
import { useBottomInset } from '../hooks/useBottomInset';

/** Room left at the end of the list so the floating button never hides the last booking. */
const LIST_END_SPACE = 96;

/**
 * Home screen: server status and the photographer's bookings, ordered by session date, with the floating button to create one.
 * @returns The screen.
 */
export default function HomeScreen(): ReactElement {
  const bottomInset = useBottomInset();
  const styles = useStyles();
  const { palette } = useAppTheme();
  const query = useBookings();
  // The maintenance pass works on every photographer's data: the backend only accepts it from system operators.
  const isSystemOperator = useSessionStore((state) => state.session?.isSystemOperator === true);
  const bookings: readonly BookingSummaryResponse[] = query.data ?? [];

  let empty: ReactElement;
  if (query.isPending) {
    empty = <ActivityIndicator color={palette.primary} accessibilityLabel="Cargando reservas" style={styles.loader} />;
  } else if (query.isError) {
    empty = (
      <Placeholder
        message="No se pudieron cargar las reservas. Revisa la conexión e inténtalo de nuevo."
        onRetry={() => void query.refetch()}
      />
    );
  } else {
    empty = <Placeholder message="Todavía no tienes reservas." />;
  }

  return (
    <View style={styles.screen}>
      <FlatList
        data={bookings}
        keyExtractor={(booking) => booking.id}
        renderItem={({ item }) => <BookingListItem booking={item} onPress={openBooking} />}
        ItemSeparatorComponent={ListSeparator}
        ListHeaderComponent={
          <View>
            <NotificationsButton />
            <RefundsButton />
            {isSystemOperator && <MaintenanceButton />}
            <Text style={styles.sectionTitle}>Mis reservas</Text>
          </View>
        }
        ListEmptyComponent={empty}
        refreshing={query.isRefetching}
        onRefresh={() => void query.refetch()}
        contentContainerStyle={[styles.container, { paddingBottom: LIST_END_SPACE + bottomInset }]}
      />
      <NewBookingFab />
    </View>
  );
}

/** Styles of the home screen. */
const useStyles = makeStyles((palette) => ({
  screen: {
    flex: 1,
  },
  container: {
    padding: spacing.md,
    paddingBottom: LIST_END_SPACE,
  },
  sectionTitle: {
    color: palette.onSurface,
    fontSize: 17,
    marginBottom: spacing.sm,
    fontFamily: FONT_FAMILIES.titleBold,
  },
  loader: {
    marginTop: spacing.lg,
  },
}));
