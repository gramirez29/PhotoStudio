import type { ReactElement } from 'react';
import { StyleSheet, Text } from 'react-native';
import { SETTLED_BOOKING_STATUSES } from '../constants/billing';
import { useSettlement } from '../hooks/useSettlement';
import { openRefund } from '../navigation/appNavigation';
import { colors, spacing } from '../theme/tokens';
import type { SettlementCardProps } from '../types/components/SettlementCard.types';
import { describeSettlement } from '../utils/billing';
import { ActionButton } from './ActionButton';
import { AppCard } from './AppCard';

/**
 * Card of the booking detail with what happened to the money when the booking ended without delivering its session: what
 * was paid, what the photographer keeps and what goes back, with the button to record the refund. It shows nothing for a
 * booking that is still active or ended with nothing paid.
 * @param props Component props.
 * @returns The card, or nothing.
 */
export function SettlementCard({ booking }: SettlementCardProps): ReactElement | null {
  const settled = SETTLED_BOOKING_STATUSES.some((status) => status === booking.status);
  const query = useSettlement(booking.id, settled);
  const settlement = query.data;

  if (!settled || settlement === undefined || settlement === null) {
    return null;
  }

  return (
    <AppCard style={styles.card}>
      <Text style={styles.title}>Dinero de esta reserva</Text>
      {describeSettlement(settlement).map((line) => (
        <Text key={line} style={styles.body}>
          {line}
        </Text>
      ))}
      {settlement.refundStatus === 'Pending' && (
        <ActionButton label="Marcar reembolso como devuelto" onPress={() => openRefund(booking.id)} />
      )}
    </AppCard>
  );
}

/** Styles of the card. */
const styles = StyleSheet.create({
  card: {
    gap: spacing.sm,
    marginTop: spacing.md,
  },
  title: {
    color: colors.textPrimary,
    fontSize: 16,
    fontWeight: '700',
  },
  body: {
    color: colors.textPrimary,
    fontSize: 15,
  },
});
