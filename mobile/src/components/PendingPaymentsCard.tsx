import type { ReactElement } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { usePaymentReview } from '../hooks/usePaymentReview';
import { colors, spacing } from '../theme/tokens';
import type { PendingPaymentsCardProps } from '../types/components/PendingPaymentsCard.types';
import { formatDateTime, formatMoney } from '../utils/format';
import { ActionButton } from './ActionButton';
import { AppCard } from './AppCard';

/**
 * Card with the payment proofs the client sent through the link and the photographer has not resolved yet. For each one it
 * shows the amount and the reference to compare with the bank, and offers to verify or reject it. It appears only when the
 * API allows verifying (`VerifyPayment`), so the app never decides by itself what a status permits.
 * @param props Component props.
 * @returns The card, or nothing when no proof is waiting.
 */
export function PendingPaymentsCard({ booking }: PendingPaymentsCardProps): ReactElement | null {
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
const styles = StyleSheet.create({
  card: {
    gap: spacing.sm,
    marginTop: spacing.md,
  },
  title: {
    color: colors.textPrimary,
    fontSize: 16,
    fontWeight: '700',
  },
  hint: {
    color: colors.textSecondary,
    fontSize: 13,
  },
  proof: {
    borderTopColor: colors.border,
    borderTopWidth: StyleSheet.hairlineWidth,
    gap: spacing.xs,
    paddingTop: spacing.sm,
  },
  amount: {
    color: colors.textPrimary,
    fontSize: 20,
    fontWeight: '700',
  },
  detail: {
    color: colors.textSecondary,
    fontSize: 14,
  },
  error: {
    color: colors.danger,
    fontSize: 14,
  },
});
