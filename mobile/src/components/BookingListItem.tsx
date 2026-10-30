import type { ReactElement } from 'react';
import { Pressable, Text, View } from 'react-native';
import { radius, spacing } from '../theme/tokens';
import type { BookingListItemProps } from '../types/components/BookingListItem.types';
import { formatDateTime, formatMoney } from '../utils/format';
import { useAppTheme } from '../hooks/useAppTheme';
import { STATUS_COLOR_ROLES } from '../utils/statusColors';
import { StatusBadge } from './StatusBadge';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * Card of the booking list: client, package, session date, status and outstanding balance. A strip at its left edge has the
 * color of the status, so the state of every booking can be read at a glance down the list.
 * @param props Component props.
 * @returns The card.
 */
export function BookingListItem({ booking, onPress }: BookingListItemProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`Reserva de ${booking.clientName}`}
      onPress={() => onPress(booking.id)}
      style={({ pressed }) => [styles.card, { borderLeftColor: palette[STATUS_COLOR_ROLES[booking.status]] }, pressed && styles.pressed]}
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
const useStyles = makeStyles((palette) => ({
  card: {
    backgroundColor: palette.surfaceVariant,
    borderLeftWidth: 6,
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
    color: palette.onSurface,
    flex: 1,
    fontSize: 17,
    fontFamily: FONT_FAMILIES.titleBold,
  },
  detail: {
    color: palette.onSurfaceVariant,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
  balance: {
    color: palette.onSurface,
    fontSize: 14,
    marginTop: spacing.xs,
    fontFamily: FONT_FAMILIES.bodyMedium,
  },
}));
