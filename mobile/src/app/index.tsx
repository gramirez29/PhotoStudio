import type { ReactElement } from 'react';
import { ActivityIndicator, FlatList, Text, View } from 'react-native';
import { BookingFilterPanel } from '../components/BookingFilterPanel';
import { BookingListItem } from '../components/BookingListItem';
import { ClearFiltersButton } from '../components/ClearFiltersButton';
import { FilterButton } from '../components/FilterButton';
import { ListSeparator } from '../components/ListSeparator';
import { LoadMoreFooter } from '../components/LoadMoreFooter';
import { NewBookingFab } from '../components/NewBookingFab';
import { Placeholder } from '../components/Placeholder';
import { useBookingFilters } from '../hooks/useBookingFilters';
import { useBookings } from '../hooks/useBookings';
import { openBooking } from '../navigation/appNavigation';
import { isFilterActive } from '../utils/bookingFilters';
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
 * Home screen: the bookings of the photographer, the most recently created first and five at a time, with the floating button
 * to create one. Above the list, the "Filtrar" button opens a panel to search by client name and to choose a status, and a broom
 * clears the filters; the results keep the same order. Notices, refunds, settings and signing out are in the side menu.
 * @returns The screen.
 */
export default function HomeScreen(): ReactElement {
  const bottomInset = useBottomInset();
  const styles = useStyles();
  const { palette } = useAppTheme();
  const filters = useBookingFilters();
  const query = useBookings(filters.filter);
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
  } else if (isFilterActive(filters.filter)) {
    empty = <Placeholder message="Ninguna reserva coincide con los filtros." />;
  } else {
    empty = <Placeholder message="Todavía no tienes reservas." />;
  }

  return (
    <View style={styles.screen}>
      <View style={styles.header}>
        <View style={styles.titleRow}>
          <Text style={styles.sectionTitle}>Mis reservas</Text>
          <View style={styles.tools}>
            {filters.activeCount > 0 && <ClearFiltersButton onPress={filters.clear} />}
            <FilterButton activeCount={filters.activeCount} open={filters.open} onPress={filters.togglePanel} />
          </View>
        </View>
        <BookingFilterPanel
          open={filters.open}
          text={filters.text}
          status={filters.status}
          onTextChange={filters.setText}
          onToggleStatus={filters.toggleStatus}
        />
      </View>
      <FlatList
        data={bookings}
        keyExtractor={(booking) => booking.id}
        renderItem={({ item }) => <BookingListItem booking={item} onPress={openBooking} />}
        ItemSeparatorComponent={ListSeparator}
        ListEmptyComponent={empty}
        ListFooterComponent={
          <LoadMoreFooter
            hasMore={query.hasNextPage}
            isLoading={query.isFetchingNextPage}
            failed={query.isFetchNextPageError}
            onLoadMore={() => void query.fetchNextPage()}
          />
        }
        keyboardShouldPersistTaps="handled"
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
  header: {
    paddingHorizontal: spacing.md,
    paddingTop: spacing.md,
  },
  titleRow: {
    alignItems: 'center',
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: spacing.sm,
  },
  tools: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: spacing.sm,
  },
  container: {
    paddingHorizontal: spacing.md,
    paddingTop: spacing.xs,
    paddingBottom: LIST_END_SPACE,
  },
  sectionTitle: {
    color: palette.onSurface,
    fontSize: 17,
    fontFamily: FONT_FAMILIES.titleBold,
  },
  loader: {
    marginTop: spacing.lg,
  },
}));
