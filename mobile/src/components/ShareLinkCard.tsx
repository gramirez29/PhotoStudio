import type { ReactElement } from 'react';
import { Text } from 'react-native';
import { useShareLink } from '../hooks/useShareLink';
import { spacing } from '../theme/tokens';
import type { ShareLinkCardProps } from '../types/components/ShareLinkCard.types';
import { ActionButton } from './ActionButton';
import { AppCard } from './AppCard';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * Card to send the client the link of the booking by WhatsApp. It is shown while the booking can still receive payments
 * (not cancelled, not expired, not marked absent). Without a client email it warns that the link only shows the booking.
 * @param props Component props.
 * @returns The card, or nothing when the booking is closed.
 */
export function ShareLinkCard({ booking }: ShareLinkCardProps): ReactElement | null {
  const styles = useStyles();
  const link = useShareLink(booking);

  if (booking.status !== 'Tentative' && booking.status !== 'Confirmed' && booking.status !== 'Completed') {
    return null;
  }

  return (
    <AppCard style={styles.card}>
      <Text style={styles.title}>Enlace del cliente</Text>
      <Text style={styles.body}>
        {booking.clientEmail === null
          ? 'Esta reserva no tiene correo del cliente: con el enlace solo podrá ver su reserva, no subir comprobantes.'
          : `El cliente puede ver su reserva y subir su comprobante. El código de verificación le llega a ${booking.clientEmail}.`}
      </Text>

      <ActionButton
        label={link.isPending ? 'Creando enlace…' : 'Enviar enlace por WhatsApp'}
        disabled={link.isPending}
        onPress={link.share}
      />

      {link.errorMessage !== null && <Text style={styles.error}>{link.errorMessage}</Text>}
    </AppCard>
  );
}

/** Styles of the card. */
const useStyles = makeStyles((palette) => ({
  card: {
    gap: spacing.sm,
    marginTop: spacing.md,
  },
  title: {
    color: palette.onSurface,
    fontSize: 16,
    fontFamily: FONT_FAMILIES.bodyBold,
  },
  body: {
    color: palette.onSurface,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
  error: {
    color: palette.error,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
}));
