import type { ReactElement } from 'react';
import { Pressable, Text, View } from 'react-native';
import { radius, spacing } from '../theme/tokens';
import type { NotificationListItemProps } from '../types/components/NotificationListItem.types';
import { formatDateTime } from '../utils/format';
import { describeNotification } from '../utils/notifications';
import { ActionButton } from './ActionButton';
import { AppCard } from './AppCard';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * Card of the inbox: what happened, when it arrived, and the actions the photographer can take (message the client on
 * WhatsApp, open the booking, mark it as read). An unread notice is marked with a dot.
 * @param props Component props.
 * @returns The card.
 */
export function NotificationListItem({
  notification,
  onSendWhatsApp,
  onOpenBooking,
  onMarkRead,
}: NotificationListItemProps): ReactElement {
  const styles = useStyles();
  const text = describeNotification(notification);
  const unread = notification.readAt === null;
  const isProof = notification.type === 'PaymentProofSubmitted';

  return (
    <AppCard style={[styles.card, unread && styles.unreadCard]}>
      <View style={styles.header}>
        <Text style={[styles.title, unread && styles.unreadTitle]}>{text.title}</Text>
        {unread && <View accessibilityLabel="Sin leer" style={styles.dot} />}
      </View>
      <Text style={styles.detail}>{text.detail}</Text>
      <Text style={styles.time}>{formatDateTime(notification.deliveredAt)}</Text>
      <View style={styles.actions}>
        {isProof ? (
          <ActionButton label="Revisar comprobante" onPress={() => onOpenBooking(notification)} />
        ) : (
          <ActionButton label="Enviar por WhatsApp" onPress={() => onSendWhatsApp(notification)} />
        )}
        <View style={styles.secondaryRow}>
          {!isProof && (
            <Pressable accessibilityRole="button" onPress={() => onOpenBooking(notification)} hitSlop={spacing.sm}>
              <Text style={styles.link}>Ver reserva</Text>
            </Pressable>
          )}
          {unread && (
            <Pressable accessibilityRole="button" onPress={() => onMarkRead(notification)} hitSlop={spacing.sm}>
              <Text style={styles.link}>Marcar como leído</Text>
            </Pressable>
          )}
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
  unreadCard: {
    borderColor: palette.primary,
    borderWidth: 1,
  },
  header: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: spacing.sm,
    justifyContent: 'space-between',
  },
  title: {
    color: palette.onSurface,
    fontSize: 16,
    fontFamily: FONT_FAMILIES.body,
  },
  unreadTitle: {
    fontFamily: FONT_FAMILIES.bodyBold,
  },
  dot: {
    backgroundColor: palette.primary,
    borderRadius: radius.pill,
    height: 10,
    width: 10,
  },
  detail: {
    color: palette.onSurfaceVariant,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
  time: {
    color: palette.muted,
    fontSize: 12,
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
