import type { ReactElement } from 'react';
import { Pressable, Text, View } from 'react-native';
import { spacing } from '../theme/tokens';
import type { RefundListItemProps } from '../types/components/RefundListItem.types';
import { SETTLEMENT_REASON_LABELS } from '../utils/billing';
import { formatDateTime, formatMoney } from '../utils/format';
import { ActionButton } from './ActionButton';
import { AppCard } from './AppCard';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * Card of the pending refunds: who is owed how much and why, with the actions to write to the client on WhatsApp, record
 * the refund as given back and open the booking.
 * @param props Component props.
 * @returns The card.
 */
export function RefundListItem({ settlement, onContact, onComplete, onOpenBooking }: RefundListItemProps): ReactElement {
  const styles = useStyles();
  return (
    <AppCard style={styles.card}>
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
    </AppCard>
  );
}

/** Styles of the card. */
const useStyles = makeStyles((palette) => ({
  card: {
    gap: spacing.xs,
  },
  header: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: spacing.sm,
    justifyContent: 'space-between',
  },
  client: {
    color: palette.onSurface,
    flex: 1,
    fontSize: 17,
    fontFamily: FONT_FAMILIES.titleBold,
  },
  amount: {
    color: palette.error,
    fontSize: 17,
    fontFamily: FONT_FAMILIES.titleBold,
  },
  detail: {
    color: palette.onSurfaceVariant,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
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
    color: palette.info,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.bodyMedium,
  },
}));
