import type { ReactElement } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { useShareLink } from '../hooks/useShareLink';
import { colors, radius, spacing } from '../theme/tokens';
import type { ShareLinkCardProps } from '../types/components/ShareLinkCard.types';
import { ActionButton } from './ActionButton';

/**
 * Card to send the client the link of the booking by WhatsApp. It is shown while the booking can still receive payments
 * (not cancelled, not expired, not marked absent). Without a client email it warns that the link only shows the booking.
 * @param props Component props.
 * @returns The card, or nothing when the booking is closed.
 */
export function ShareLinkCard({ booking }: ShareLinkCardProps): ReactElement | null {
  const link = useShareLink(booking);

  if (booking.status !== 'Tentative' && booking.status !== 'Confirmed' && booking.status !== 'Completed') {
    return null;
  }

  return (
    <View style={styles.card}>
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
    </View>
  );
}

/** Styles of the card. */
const styles = StyleSheet.create({
  card: {
    backgroundColor: colors.surface,
    borderRadius: radius.md,
    gap: spacing.sm,
    marginTop: spacing.md,
    padding: spacing.md,
  },
  title: {
    color: colors.textPrimary,
    fontSize: 16,
    fontWeight: '700',
  },
  body: {
    color: colors.textPrimary,
    fontSize: 14,
  },
  error: {
    color: colors.danger,
    fontSize: 14,
  },
});
