import type { ReactElement } from 'react';
import { Alert, StyleSheet, Text } from 'react-native';
import { env } from '../config/env';
import { openAgenda, openAllBookings, openNewBooking } from '../navigation/appNavigation';
import { openExternalUrl } from '../navigation/externalLinks';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';
import { AppCard } from './AppCard';
import { HomeLinkRow } from './HomeLinkRow';

/**
 * Opens the web area of the photographer in the browser of the phone, and says so when the phone could not open it.
 * @param url Address of the web area.
 * @returns A promise that resolves when the attempt finished.
 */
async function openStudioWeb(url: string): Promise<void> {
  if (!(await openExternalUrl(url))) {
    Alert.alert('No se pudo abrir el enlace', 'Abre la web del fotógrafo desde el navegador del teléfono.');
  }
}

/**
 * The card of the home screen with the ways to go on: the agenda, all the bookings, a new booking and, when the app knows its
 * address, the web area of the photographer, which opens in the browser of the phone.
 * @returns The card.
 */
export function HomeShortcutsCard(): ReactElement {
  const styles = useStyles();
  const studioUrl = env.studioUrl;

  return (
    <AppCard style={layout.card}>
      <Text accessibilityRole="header" style={styles.title}>
        Ir a
      </Text>
      <HomeLinkRow icon="event" label="Agenda" onPress={openAgenda} />
      <HomeLinkRow icon="list-alt" label="Todas las reservas" onPress={() => openAllBookings()} />
      <HomeLinkRow icon="add-circle-outline" label="Nueva reserva" onPress={openNewBooking} />
      {studioUrl !== null && (
        <HomeLinkRow icon="open-in-new" label="Abrir la web del fotógrafo" onPress={() => void openStudioWeb(studioUrl)} />
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
}));
