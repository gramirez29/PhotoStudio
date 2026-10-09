import type { ReactElement } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { colors, radius, spacing } from '../theme/tokens';
import type { NotificationListItemProps } from '../types/components/NotificationListItem.types';
import { formatDateTime } from '../utils/format';
import { describeNotification } from '../utils/notifications';
import { ActionButton } from './ActionButton';

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
  const text = describeNotification(notification);
  const unread = notification.readAt === null;

  return (
    <View style={[styles.card, unread && styles.unreadCard]}>
      <View style={styles.header}>
        <Text style={[styles.title, unread && styles.unreadTitle]}>{text.title}</Text>
        {unread && <View accessibilityLabel="Sin leer" style={styles.dot} />}
      </View>
      <Text style={styles.detail}>{text.detail}</Text>
      <Text style={styles.time}>{formatDateTime(notification.deliveredAt)}</Text>
      <View style={styles.actions}>
        <ActionButton label="Enviar por WhatsApp" onPress={() => onSendWhatsApp(notification)} />
        <View style={styles.secondaryRow}>
          <Pressable accessibilityRole="button" onPress={() => onOpenBooking(notification)} hitSlop={spacing.sm}>
            <Text style={styles.link}>Ver reserva</Text>
          </Pressable>
          {unread && (
            <Pressable accessibilityRole="button" onPress={() => onMarkRead(notification)} hitSlop={spacing.sm}>
              <Text style={styles.link}>Marcar como leído</Text>
            </Pressable>
          )}
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
  unreadCard: {
    borderColor: colors.primary,
    borderWidth: 1,
  },
  header: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: spacing.sm,
    justifyContent: 'space-between',
  },
  title: {
    color: colors.textPrimary,
    fontSize: 16,
  },
  unreadTitle: {
    fontWeight: '700',
  },
  dot: {
    backgroundColor: colors.primary,
    borderRadius: radius.pill,
    height: 10,
    width: 10,
  },
  detail: {
    color: colors.textSecondary,
    fontSize: 14,
  },
  time: {
    color: colors.muted,
    fontSize: 12,
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
