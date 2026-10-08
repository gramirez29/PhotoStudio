import type { ReactElement } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { colors, radius, spacing } from '../theme/tokens';
import type { BookingListItemProps } from '../types/components/BookingListItem.types';
import { formatDateTime, formatMoney } from '../utils/format';
import { StatusBadge } from './StatusBadge';

/**
 * Card of the booking list: client, package, session date, status and outstanding balance.
 * @param props Component props.
 * @returns The card.
 */
export function BookingListItem({ booking, onPress }: BookingListItemProps): ReactElement {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`Reserva de ${booking.clientName}`}
      onPress={() => onPress(booking.id)}
      style={({ pressed }) => [styles.card, pressed && styles.pressed]}
    >
      <View style={styles.header}>
        <Text style={styles.client} numberOfLines={1}>
          {booking.clientName}
        </Text>
        <StatusBadge status={booking.status} />
      </View>
      <Text style={styles.detail} numberOfLines={1}>
        {booking.packageName}
      </Text>
      <Text style={styles.detail}>{formatDateTime(booking.sessionStart)}</Text>
      <Text style={styles.balance}>Saldo: {formatMoney(booking.balance)}</Text>
    </Pressable>
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
  pressed: {
    opacity: 0.8,
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
  detail: {
    color: colors.textSecondary,
    fontSize: 14,
  },
  balance: {
    color: colors.textPrimary,
    fontSize: 14,
    fontWeight: '600',
    marginTop: spacing.xs,
  },
});
