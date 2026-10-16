import type { ReactElement } from 'react';
import { StyleSheet, Text } from 'react-native';
import { useRejectPaymentForm } from '../hooks/useRejectPaymentForm';
import { colors } from '../theme/tokens';
import type { RejectPaymentFormProps } from '../types/components/RejectPaymentForm.types';
import { formatDateTime, formatMoney } from '../utils/format';
import { ActionButton } from './ActionButton';
import { FormInput } from './FormInput';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';

/**
 * Form shown once the booking is loaded: which proof is being rejected and the reason field. The proof stays on record
 * with its reason; it just stops waiting for verification.
 * @param props Component props.
 * @returns The form.
 */
export function RejectPaymentForm({ booking, payment }: RejectPaymentFormProps): ReactElement {
  const form = useRejectPaymentForm(booking.id, payment.id);

  return (
    <KeyboardAwareScreen align="top">
      <Text style={styles.title}>{booking.clientName}</Text>
      <Text style={styles.subtitle}>
        Comprobante de {formatMoney(payment.amount)} · {formatDateTime(payment.recordedAt)}
      </Text>
      {payment.reference !== null && <Text style={styles.subtitle}>Referencia: {payment.reference}</Text>}

      <FormInput
        label="Motivo del rechazo"
        value={form.text}
        onChangeText={form.setText}
        error={form.validationError ?? undefined}
        placeholder="Por ejemplo: no aparece la transferencia en mi banco"
        multiline
        numberOfLines={3}
        autoCapitalize="sentences"
      />
      <Text style={styles.hint}>
        Es obligatorio. El comprobante queda guardado con este motivo y no cuenta como pago. Después puedes escribirle al cliente por WhatsApp.
      </Text>

      {form.submitError !== null && <Text style={styles.submitError}>{form.submitError}</Text>}

      <ActionButton
        label={form.isPending ? 'Enviando…' : 'Rechazar comprobante'}
        variant="danger"
        disabled={form.isPending}
        onPress={form.submit}
      />
    </KeyboardAwareScreen>
  );
}

/** Styles of the form. */
const styles = StyleSheet.create({
  title: {
    color: colors.textPrimary,
    fontSize: 22,
    fontWeight: '700',
  },
  subtitle: {
    color: colors.textSecondary,
    fontSize: 15,
  },
  hint: {
    color: colors.textSecondary,
    fontSize: 13,
  },
  submitError: {
    color: colors.danger,
    fontSize: 14,
  },
});
