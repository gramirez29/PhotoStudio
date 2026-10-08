import type { ReactElement } from 'react';
import { ActivityIndicator, FlatList, StyleSheet, Text, View } from 'react-native';
import { ApiStatus } from '../components/ApiStatus';
import { BookingListItem } from '../components/BookingListItem';
import { ListSeparator } from '../components/ListSeparator';
import { Placeholder } from '../components/Placeholder';
import { env } from '../config/env';
import { useBookings } from '../hooks/useBookings';
import { colors, spacing } from '../theme/tokens';
import type { BookingSummaryResponse } from '../types/api/booking';
import { openBooking } from '../utils/navigation';

/**
 * Home screen: server status and the photographer's bookings, ordered by session date.
 * @returns The screen.
 */
export default function HomeScreen(): ReactElement {
  const query = useBookings(env.photographerId);
  const bookings: readonly BookingSummaryResponse[] = query.data ?? [];

  let empty: ReactElement;
  if (env.photographerId === null) {
    empty = <Placeholder message="Falta configurar EXPO_PUBLIC_PHOTOGRAPHER_ID en el archivo .env de la app." />;
  } else if (query.isPending) {
    empty = <ActivityIndicator color={colors.primary} accessibilityLabel="Cargando reservas" style={styles.loader} />;
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
    <FlatList
      data={bookings}
      keyExtractor={(booking) => booking.id}
      renderItem={({ item }) => <BookingListItem booking={item} onPress={openBooking} />}
      ItemSeparatorComponent={ListSeparator}
      ListHeaderComponent={
        <View>
          <ApiStatus />
          <Text style={styles.sectionTitle}>Mis reservas</Text>
        </View>
      }
      ListEmptyComponent={empty}
      refreshing={query.isRefetching}
      onRefresh={() => void query.refetch()}
      contentContainerStyle={styles.container}
    />
  );
}

/** Styles of the home screen. */
const styles = StyleSheet.create({
  container: {
    padding: spacing.md,
  },
  sectionTitle: {
    color: colors.textPrimary,
    fontSize: 17,
    fontWeight: '700',
    marginBottom: spacing.sm,
  },
  loader: {
    marginTop: spacing.lg,
  },
});
