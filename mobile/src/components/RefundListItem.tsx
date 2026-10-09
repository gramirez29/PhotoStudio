import type { ReactElement } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { colors, radius, spacing } from '../theme/tokens';
import type { RefundListItemProps } from '../types/components/RefundListItem.types';
import { SETTLEMENT_REASON_LABELS } from '../utils/billing';
import { formatDateTime, formatMoney } from '../utils/format';
import { ActionButton } from './ActionButton';

/**
 * Card of the pending refunds: who is owed how much and why, with the actions to write to the client on WhatsApp, record
 * the refund as given back and open the booking.
 * @param props Component props.
 * @returns The card.
 */
export function RefundListItem({ settlement, onContact, onComplete, onOpenBooking }: RefundListItemProps): ReactElement {
  return (
    <View style={styles.card}>
      <View style={styles.header}>
        <Text style={styles.client} numberOfLines={1}>
          {settlement.clientName}
        </Text>
        <Text style={styles.amount}>{formatMoney(settlement.refundAmount)}</Text>
      </View>
      <Text style={styles.detail}>{SETTLEMENT_REASON_LABELS[settlement.reason]}</Text>
      <Text style={styles.detail} numberOfLines={1}>
        {settlement.packageName} · {formatDateTime(settlement.sessionStart)}
      </Text>
      {settlement.retentionStatus === 'Applied' && (
        <Text style={styles.detail}>Te quedas con {formatMoney(settlement.retained)} del anticipo.</Text>
      )}

      <View style={styles.actions}>
        <ActionButton label="Marcar como devuelto" onPress={() => onComplete(settlement)} />
        <View style={styles.secondaryRow}>
          <Pressable accessibilityRole="button" onPress={() => onContact(settlement)} hitSlop={spacing.sm}>
            <Text style={styles.link}>Escribir por WhatsApp</Text>
          </Pressable>
          <Pressable accessibilityRole="button" onPress={() => onOpenBooking(settlement)} hitSlop={spacing.sm}>
            <Text style={styles.link}>Ver reserva</Text>
          </Pressable>
        </View>
      </View>
    </View>
  );
}

/** Styles of the card. */
const styles = StyleSheet.create({
  card: {
    backgroundColor: colors.surface,
    borderRadius: radius.md,
    gap: spacing.xs,
    padding: spacing.md,
  },
  header: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: spacing.sm,
    justifyContent: 'space-between',
  },
  client: {
    color: colors.textPrimary,
    flex: 1,
    fontSize: 17,
    fontWeight: '700',
  },
  amount: {
    color: colors.danger,
    fontSize: 17,
    fontWeight: '700',
  },
  detail: {
    color: colors.textSecondary,
    fontSize: 14,
  },
  actions: {
    gap: spacing.sm,
    marginTop: spacing.sm,
  },
  secondaryRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  link: {
    color: colors.info,
    fontSize: 14,
    fontWeight: '600',
  },
});
