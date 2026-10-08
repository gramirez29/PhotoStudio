import { useLocalSearchParams } from 'expo-router';
import { useEffect, type ReactElement } from 'react';
import { ActivityIndicator, ScrollView, StyleSheet, Text, View } from 'react-native';
import { ApiError } from '../../api/httpClient';
import type { BookingResponse } from '../../api/types';
import { AmountRow } from '../../components/AmountRow';
import { StatusBadge } from '../../components/StatusBadge';
import { useBooking } from '../../hooks/useBooking';
import { useRecentBookingsStore } from '../../state/recentBookingsStore';
import { colors, radius, spacing } from '../../theme/tokens';
import { formatDateTime } from '../../utils/format';
import { BOOKING_ACTION_LABELS } from '../../utils/labels';

/**
 * Converts a query error into a message for the photographer.
 * @param error Error of the query.
 * @returns The message.
 */
function errorMessage(error: Error): string {
  if (error instanceof ApiError && error.status === 404) {
    return 'No existe una reserva con ese identificador.';
  }

  return 'No se pudo cargar la reserva. Revisa la conexión e inténtalo de nuevo.';
}

/** Props of {@link BookingDetails}. */
interface BookingDetailsProps {
  /** Booking to display. */
  readonly booking: BookingResponse;
}

/**
 * Body of the screen once the booking is loaded.
 * @param props Component props.
 * @returns The booking details.
 */
function BookingDetails({ booking }: BookingDetailsProps): ReactElement {
  return (
    <ScrollView contentContainerStyle={styles.container}>
      <StatusBadge status={booking.status} />
      <Text style={styles.title}>{booking.clientName}</Text>
      <Text style={styles.subtitle}>{booking.packageName}</Text>
      <Text style={styles.subtitle}>{formatDateTime(booking.sessionStart)}</Text>
      {booking.expiresAt !== null && (
        <Text style={styles.warning}>Se libera el espacio el {formatDateTime(booking.expiresAt)}</Text>
      )}

      <View style={styles.card}>
        <AmountRow label="Precio del paquete" money={booking.packagePrice} />
        <AmountRow label="Anticipo requerido" money={booking.depositRequired} />
        <AmountRow label="Pagado" money={booking.totalPaid} />
        <AmountRow label="Saldo" money={booking.balance} emphasized />
      </View>

      <View style={styles.card}>
        <Text style={styles.cardTitle}>Contrato</Text>
        <Text style={styles.body}>
          {booking.contract === null
            ? 'Pendiente de firma'
            : `Firmado por ${booking.contract.signerName} el ${formatDateTime(booking.contract.signedAt)}`}
        </Text>
      </View>

      <View style={styles.card}>
        <Text style={styles.cardTitle}>Acciones disponibles</Text>
        {booking.allowedActions.length === 0 ? (
          <Text style={styles.body}>No hay acciones disponibles en este estado.</Text>
        ) : (
          booking.allowedActions.map((action) => (
            <Text key={action} style={styles.body}>
              • {BOOKING_ACTION_LABELS[action]}
            </Text>
          ))
        )}
      </View>
    </ScrollView>
  );
}

/**
 * Booking detail screen. Remembers the booking in the recent list once it loads.
 * @returns The screen.
 */
export default function BookingScreen(): ReactElement {
  const { id } = useLocalSearchParams<{ id: string }>();
  const bookingId = typeof id === 'string' ? id : '';
  const query = useBooking(bookingId);
  const addBooking = useRecentBookingsStore((state) => state.addBooking);

  useEffect(() => {
    if (query.data !== undefined) {
      addBooking(query.data.id);
    }
  }, [addBooking, query.data]);

  if (query.isPending) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator color={colors.primary} accessibilityLabel="Cargando reserva" />
      </View>
    );
  }

  if (query.isError) {
    return (
      <View style={styles.centered}>
        <Text style={styles.body}>{errorMessage(query.error)}</Text>
      </View>
    );
  }

  return <BookingDetails booking={query.data} />;
}

/** Styles of the booking screen. */
const styles = StyleSheet.create({
  container: {
    padding: spacing.md,
    gap: spacing.sm,
  },
  centered: {
    alignItems: 'center',
    flex: 1,
    justifyContent: 'center',
    padding: spacing.lg,
  },
  title: {
    color: colors.textPrimary,
    fontSize: 22,
    fontWeight: '700',
    marginTop: spacing.sm,
  },
  subtitle: {
    color: colors.textSecondary,
    fontSize: 15,
  },
  warning: {
    color: colors.warning,
    fontSize: 14,
  },
  card: {
    backgroundColor: colors.surface,
    borderRadius: radius.md,
    gap: spacing.xs,
    marginTop: spacing.md,
    padding: spacing.md,
  },
  cardTitle: {
    color: colors.textPrimary,
    fontSize: 16,
    fontWeight: '700',
  },
  body: {
    color: colors.textPrimary,
    fontSize: 15,
  },
});
