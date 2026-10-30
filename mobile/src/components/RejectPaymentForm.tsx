import type { ReactElement } from 'react';
import { Text } from 'react-native';
import { useRejectPaymentForm } from '../hooks/useRejectPaymentForm';
import type { RejectPaymentFormProps } from '../types/components/RejectPaymentForm.types';
import { formatDateTime, formatMoney } from '../utils/format';
import { ActionButton } from './ActionButton';
import { FormInput } from './FormInput';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * Form shown once the booking is loaded: which proof is being rejected and the reason field. The proof stays on record
 * with its reason; it just stops waiting for verification.
 * @param props Component props.
 * @returns The form.
 */
export function RejectPaymentForm({ booking, payment }: RejectPaymentFormProps): ReactElement {
  const styles = useStyles();
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
const useStyles = makeStyles((palette) => ({
  title: {
    color: palette.onSurface,
    fontSize: 22,
    fontFamily: FONT_FAMILIES.titleBold,
  },
  subtitle: {
    color: palette.onSurfaceVariant,
    fontSize: 15,
    fontFamily: FONT_FAMILIES.body,
  },
  hint: {
    color: palette.onSurfaceVariant,
    fontSize: 13,
    fontFamily: FONT_FAMILIES.body,
  },
  submitError: {
    color: palette.error,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
}));
