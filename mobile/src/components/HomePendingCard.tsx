import type { ReactElement } from 'react';
import { StyleSheet, Text } from 'react-native';
import { useMenuCounters } from '../hooks/useMenuCounters';
import { openNotifications, openRefunds } from '../navigation/appNavigation';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';
import { refundsLabel } from '../utils/billing';
import { inboxLabel } from '../utils/notifications';
import { AppCard } from './AppCard';
import { HomeLinkRow } from './HomeLinkRow';

/**
 * The card of the home screen with what is waiting for the photographer: the notices not read yet and the refunds still owed, each a
 * row that opens its screen. When nothing is waiting it says so, so an empty card is never a doubt.
 * @returns The card.
 */
export function HomePendingCard(): ReactElement {
  const styles = useStyles();
  const { unreadCount, pendingRefunds, needsAttention } = useMenuCounters();

  return (
    <AppCard style={layout.card}>
      <Text accessibilityRole="header" style={styles.title}>
        Pendientes
      </Text>
      {!needsAttention && <Text style={styles.empty}>No tienes nada pendiente.</Text>}
      {unreadCount > 0 && (
        <HomeLinkRow
          icon="notifications-none"
          label="Avisos sin leer"
          detail={String(unreadCount)}
          accessibilityLabel={inboxLabel(unreadCount)}
          onPress={openNotifications}
        />
      )}
      {pendingRefunds > 0 && (
        <HomeLinkRow
          icon="payments"
          label="Reembolsos pendientes"
          detail={String(pendingRefunds)}
          accessibilityLabel={refundsLabel(pendingRefunds)}
          onPress={openRefunds}
        />
      )}
    </AppCard>
  );
}

/** Layout of the card. */
const layout = StyleSheet.create({
  card: {
    gap: spacing.xs,
  },
});

/** Styles of the card: the colors come from the theme. */
const useStyles = makeStyles((palette) => ({
  title: {
    color: palette.onSurface,
    fontFamily: FONT_FAMILIES.titleBold,
    fontSize: 20,
  },
  empty: {
    color: palette.onSurfaceVariant,
    fontFamily: FONT_FAMILIES.body,
    fontSize: 15,
    paddingVertical: spacing.xs,
  },
}));
