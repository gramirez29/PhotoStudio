import type { ReactElement } from 'react';
import { StyleSheet, Text } from 'react-native';
import { useArchivableGalleries } from '../hooks/useArchivableGalleries';
import { useBookingAttention } from '../hooks/useBookingAttention';
import { useMenuCounters } from '../hooks/useMenuCounters';
import { openAllBookings, openArchivableGalleries, openNotifications, openRefunds } from '../navigation/appNavigation';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';
import { refundsLabel } from '../utils/billing';
import { archivableLabel, expiringSoonLabel, pendingProofsLabel } from '../utils/home/labels';
import { inboxLabel } from '../utils/notifications';
import { AppCard } from './AppCard';
import { HomeLinkRow } from './HomeLinkRow';

/**
 * The card of the home screen with what is waiting for the photographer, each a row with its count that opens where it is dealt with:
 * the notices not read, the refunds still owed, the bookings with a proof of payment to verify (each has a notice with a button to
 * review it), the tentative bookings that expire within 24 hours (the list of bookings, already filtered by that status) and the
 * delivered galleries whose space can be freed. A row appears only when it has something; when nothing is waiting the card says so.
 * @returns The card.
 */
export function HomePendingCard(): ReactElement {
  const styles = useStyles();
  const { unreadCount, pendingRefunds } = useMenuCounters();
  const attention = useBookingAttention().data;
  const pendingProofs = attention?.pendingProofs ?? 0;
  const expiringSoon = attention?.expiringSoon ?? 0;
  const archivable = useArchivableGalleries().data?.length ?? 0;
  const nothing = unreadCount + pendingRefunds + pendingProofs + expiringSoon + archivable === 0;

  return (
    <AppCard style={layout.card}>
      <Text accessibilityRole="header" style={styles.title}>
        Pendientes
      </Text>
      {nothing && <Text style={styles.empty}>No tienes nada pendiente.</Text>}
      {unreadCount > 0 && (
        <HomeLinkRow
          icon="notifications-none"
          label="Avisos sin leer"
          detail={String(unreadCount)}
          accessibilityLabel={inboxLabel(unreadCount)}
          onPress={openNotifications}
        />
      )}
      {pendingProofs > 0 && (
        <HomeLinkRow
          icon="receipt-long"
          label="Comprobantes por verificar"
          detail={String(pendingProofs)}
          accessibilityLabel={pendingProofsLabel(pendingProofs)}
          onPress={openNotifications}
        />
      )}
      {expiringSoon > 0 && (
        <HomeLinkRow
          icon="hourglass-bottom"
          label="Tentativas por vencer en 24 h"
          detail={String(expiringSoon)}
          accessibilityLabel={expiringSoonLabel(expiringSoon)}
          onPress={() => openAllBookings('Tentative')}
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
      {archivable > 0 && (
        <HomeLinkRow
          icon="inventory-2"
          label="Galerías por archivar"
          detail={String(archivable)}
          accessibilityLabel={archivableLabel(archivable)}
          onPress={openArchivableGalleries}
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
