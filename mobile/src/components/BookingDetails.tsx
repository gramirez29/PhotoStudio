import type { ReactElement } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { colors, radius, spacing } from '../theme/tokens';
import type { BookingDetailsProps } from '../types/components/BookingDetails.types';
import { formatDateTime } from '../utils/format';
import { AmountRow } from './AmountRow';
import { BookingActions } from './BookingActions';
import { StatusBadge } from './StatusBadge';

/**
 * Body of the booking screen once the booking is loaded: status, client, amounts, contract and available actions.
 * @param props Component props.
 * @returns The booking details.
 */
export function BookingDetails({ booking }: BookingDetailsProps): ReactElement {
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

      <BookingActions booking={booking} />
    </ScrollView>
  );
}

/** Styles of the booking details. */
const styles = StyleSheet.create({
  container: {
    padding: spacing.md,
    gap: spacing.sm,
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
