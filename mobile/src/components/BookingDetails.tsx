import type { ReactElement } from 'react';
import { ScrollView, StyleSheet, Text } from 'react-native';
import { colors, spacing } from '../theme/tokens';
import type { BookingDetailsProps } from '../types/components/BookingDetails.types';
import { formatDateTime } from '../utils/format';
import { AmountRow } from './AmountRow';
import { BookingActions } from './BookingActions';
import { ContractCard } from './ContractCard';
import { PendingPaymentsCard } from './PendingPaymentsCard';
import { SettlementCard } from './SettlementCard';
import { ShareLinkCard } from './ShareLinkCard';
import { StatusBadge } from './StatusBadge';
import { AppCard } from './AppCard';

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
      {booking.clientEmail !== null && <Text style={styles.subtitle}>{booking.clientEmail}</Text>}
      {booking.expiresAt !== null && (
        <Text style={styles.warning}>Se libera el espacio el {formatDateTime(booking.expiresAt)}</Text>
      )}

      <AppCard style={styles.card}>
        <AmountRow label="Precio del paquete" money={booking.basePrice} />
        {booking.extras.map((extra) => (
          <AmountRow key={extra.code} label={`${extra.name} × ${extra.quantity}`} money={extra.total} />
        ))}
        {booking.discount.amount > 0 && <AmountRow label="Descuento" money={{ amount: -booking.discount.amount, currency: booking.discount.currency }} />}
        {(booking.extras.length > 0 || booking.discount.amount > 0) && <AmountRow label="Total" money={booking.packagePrice} emphasized />}
        <AmountRow label="Anticipo requerido" money={booking.depositRequired} />
        <AmountRow label="Pagado" money={booking.totalPaid} />
        <AmountRow label="Saldo" money={booking.balance} emphasized />
      </AppCard>

      <ContractCard booking={booking} />

      <PendingPaymentsCard booking={booking} />

      <SettlementCard booking={booking} />

      <BookingActions booking={booking} />

      <ShareLinkCard booking={booking} />
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
    gap: spacing.xs,
    marginTop: spacing.md,
  },
});
