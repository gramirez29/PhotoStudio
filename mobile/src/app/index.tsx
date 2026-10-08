import { router } from 'expo-router';
import type { ReactElement } from 'react';
import { ActivityIndicator, FlatList, Pressable, StyleSheet, Text, View } from 'react-native';
import type { BookingSummaryResponse } from '../api/types';
import { BookingListItem } from '../components/BookingListItem';
import { env } from '../config/env';
import { useApiHealth } from '../hooks/useApiHealth';
import { useBookings } from '../hooks/useBookings';
import { colors, radius, spacing } from '../theme/tokens';

/**
 * Opens the detail screen of a booking.
 * @param bookingId Booking identifier.
 */
function openBooking(bookingId: string): void {
  router.push({ pathname: '/bookings/[id]', params: { id: bookingId } });
}

/**
 * Shows whether the backend is reachable and ready.
 * @returns The status line.
 */
function ApiStatus(): ReactElement {
  const health = useApiHealth();

  let label = 'Verificando conexión con el servidor…';
  let color: string = colors.textSecondary;

  if (health.isError) {
    label = 'Sin conexión con el servidor';
    color = colors.danger;
  } else if (health.data === true) {
    label = 'Servidor conectado';
    color = colors.success;
  } else if (health.data === false) {
    label = 'Servidor disponible, base de datos sin conexión';
    color = colors.warning;
  }

  return <Text style={[styles.status, { color }]}>{label}</Text>;
}

/** Props of {@link Placeholder}. */
interface PlaceholderProps {
  /** Message shown to the photographer. */
  readonly message: string;
  /** Optional retry handler; when present a button is shown. */
  readonly onRetry?: () => void;
}

/**
 * Centered message for the states without bookings to show (empty, error, not configured).
 * @param props Component props.
 * @returns The message block.
 */
function Placeholder({ message, onRetry }: PlaceholderProps): ReactElement {
  return (
    <View style={styles.placeholder}>
      <Text style={styles.placeholderText}>{message}</Text>
      {onRetry !== undefined && (
        <Pressable
          accessibilityRole="button"
          onPress={onRetry}
          style={({ pressed }) => [styles.button, pressed && styles.buttonPressed]}
        >
          <Text style={styles.buttonLabel}>Reintentar</Text>
        </Pressable>
      )}
    </View>
  );
}

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
      ItemSeparatorComponent={Separator}
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

/**
 * Vertical gap between two cards.
 * @returns The spacer.
 */
function Separator(): ReactElement {
  return <View style={styles.separator} />;
}

/** Styles of the home screen. */
const styles = StyleSheet.create({
  container: {
    padding: spacing.md,
  },
  status: {
    fontSize: 14,
    marginBottom: spacing.md,
  },
  sectionTitle: {
    color: colors.textPrimary,
    fontSize: 17,
    fontWeight: '700',
    marginBottom: spacing.sm,
  },
  separator: {
    height: spacing.sm,
  },
  loader: {
    marginTop: spacing.lg,
  },
  placeholder: {
    alignItems: 'center',
    gap: spacing.md,
    paddingVertical: spacing.lg,
  },
  placeholderText: {
    color: colors.textSecondary,
    fontSize: 15,
    textAlign: 'center',
  },
  button: {
    alignItems: 'center',
    backgroundColor: colors.primary,
    borderRadius: radius.sm,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
  },
  buttonPressed: {
    opacity: 0.8,
  },
  buttonLabel: {
    color: colors.textInverse,
    fontSize: 16,
    fontWeight: '600',
  },
});
