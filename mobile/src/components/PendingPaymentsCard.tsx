import type { ReactElement } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { usePaymentReview } from '../hooks/usePaymentReview';
import { spacing } from '../theme/tokens';
import type { PendingPaymentsCardProps } from '../types/components/PendingPaymentsCard.types';
import { formatDateTime, formatMoney } from '../utils/format';
import { ActionButton } from './ActionButton';
import { AppCard } from './AppCard';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * Card with the payment proofs the client sent through the link and the photographer has not resolved yet. For each one it
 * shows the amount and the reference to compare with the bank, and offers to verify or reject it. It appears only when the
 * API allows verifying (`VerifyPayment`), so the app never decides by itself what a status permits.
 * @param props Component props.
 * @returns The card, or nothing when no proof is waiting.
 */
export function PendingPaymentsCard({ booking }: PendingPaymentsCardProps): ReactElement | null {
  const styles = useStyles();
  const review = usePaymentReview(booking);
  const pending = booking.payments.filter((payment) => payment.status === 'PendingVerification');

  if (!booking.allowedActions.includes('VerifyPayment') || pending.length === 0) {
    return null;
  }

  return (
    <AppCard style={styles.card}>
      <Text style={styles.title}>Comprobantes por verificar</Text>
      <Text style={styles.hint}>Compara el monto y la referencia con tu banco antes de verificar.</Text>

      {pending.map((payment) => (
        <View key={payment.id} style={styles.proof}>
          <Text style={styles.amount}>{formatMoney(payment.amount)}</Text>
          <Text style={styles.detail}>Enviado el {formatDateTime(payment.recordedAt)}</Text>
          <Text style={styles.detail}>
            {payment.reference === null ? 'Sin referencia' : `Referencia: ${payment.reference}`}
          </Text>
          <ActionButton label="Verificar pago" disabled={review.isPending} onPress={() => review.verify(payment)} />
          <ActionButton
            label="Rechazar comprobante"
            variant="danger"
            disabled={review.isPending}
            onPress={() => review.reject(payment)}
          />
        </View>
      ))}

      {review.errorMessage !== null && <Text style={styles.error}>{review.errorMessage}</Text>}
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
  hint: {
    color: palette.onSurfaceVariant,
    fontSize: 13,
    fontFamily: FONT_FAMILIES.body,
  },
  proof: {
    borderTopColor: palette.outlineVariant,
    borderTopWidth: StyleSheet.hairlineWidth,
    gap: spacing.xs,
    paddingTop: spacing.sm,
  },
  amount: {
    color: palette.onSurface,
    fontSize: 20,
    fontFamily: FONT_FAMILIES.titleBold,
  },
  detail: {
    color: palette.onSurfaceVariant,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
  error: {
    color: palette.error,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
}));
