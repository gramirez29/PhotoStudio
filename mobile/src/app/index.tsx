import type { ReactElement } from 'react';
import { ActivityIndicator, FlatList, Text, View } from 'react-native';
import { BookingListItem } from '../components/BookingListItem';
import { ListSeparator } from '../components/ListSeparator';
import { LoadMoreFooter } from '../components/LoadMoreFooter';
import { NewBookingFab } from '../components/NewBookingFab';
import { Placeholder } from '../components/Placeholder';
import { useBookings } from '../hooks/useBookings';
import { openBooking } from '../navigation/appNavigation';
import { flattenPages } from '../utils/pagination';
import { spacing } from '../theme/tokens';
import type { BookingSummaryResponse } from '../types/api/booking';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { useAppTheme } from '../hooks/useAppTheme';
import { useBottomInset } from '../hooks/useBottomInset';

/** Room left at the end of the list so the floating button never hides the last booking. */
const LIST_END_SPACE = 96;

/**
 * Home screen: the photographer's bookings, the most recently created first and five at a time, with the floating button to
 * create one. Notices, refunds,
 * settings and signing out are in the side menu.
 * @returns The screen.
 */
export default function HomeScreen(): ReactElement {
  const bottomInset = useBottomInset();
  const styles = useStyles();
  const { palette } = useAppTheme();
  const query = useBookings();
  const bookings: readonly BookingSummaryResponse[] = flattenPages(query.data?.pages);

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
        ListHeaderComponent={<Text style={styles.sectionTitle}>Mis reservas</Text>}
        ListEmptyComponent={empty}
        ListFooterComponent={
          <LoadMoreFooter
            hasMore={query.hasNextPage}
            isLoading={query.isFetchingNextPage}
            failed={query.isFetchNextPageError}
            onLoadMore={() => void query.fetchNextPage()}
          />
        }
        refreshing={query.isRefetching && !query.isFetchingNextPage}
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
