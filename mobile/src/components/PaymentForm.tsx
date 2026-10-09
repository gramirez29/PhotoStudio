import type { ReactElement } from 'react';
import { StyleSheet, Text } from 'react-native';
import { usePaymentForm } from '../hooks/usePaymentForm';
import { colors } from '../theme/tokens';
import type { PaymentFormProps } from '../types/components/PaymentForm.types';
import { formatDateTime, formatMoney } from '../utils/format';
import { IN_PERSON_PAYMENT_LABELS, PAYMENT_METHOD_LABELS, inPersonMethodFromLabel } from '../utils/payments';
import { ActionButton } from './ActionButton';
import { FormInput } from './FormInput';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';
import { SelectField } from './SelectField';

/**
 * Form shown once the booking is loaded: what is owed, the amount and method received, and the button that records the
 * payment.
 * @param props Component props.
 * @returns The form.
 */
export function PaymentForm({ booking }: PaymentFormProps): ReactElement {
  const form = usePaymentForm(booking);

  return (
    <KeyboardAwareScreen align="top">
      <Text style={styles.title}>{booking.clientName}</Text>
      <Text style={styles.subtitle}>
        {booking.packageName} · {formatDateTime(booking.sessionStart)}
      </Text>
      <Text style={styles.summary}>Anticipo requerido: {formatMoney(booking.depositRequired)}</Text>
      <Text style={styles.summary}>Pagado: {formatMoney(booking.totalPaid)}</Text>
      <Text style={styles.summary}>Saldo: {formatMoney(booking.balance)}</Text>

      <FormInput
        label="Monto recibido (₡)"
        value={form.amount}
        onChangeText={form.setAmount}
        error={form.validationError ?? undefined}
        keyboardType="number-pad"
        placeholder="50000"
      />
      <SelectField
        label="Método de pago"
        options={IN_PERSON_PAYMENT_LABELS}
        value={PAYMENT_METHOD_LABELS[form.method]}
        placeholder="Elige el método"
        onChange={(label) => {
          const method = inPersonMethodFromLabel(label);
          if (method !== null) {
            form.setMethod(method);
          }
        }}
      />
      <Text style={styles.hint}>
        El pago queda verificado de inmediato. Cuando hay contrato firmado y el anticipo está cubierto, la reserva pasa a
        confirmada. Un pago registrado no se puede editar.
      </Text>

      {form.submitError !== null && <Text style={styles.submitError}>{form.submitError}</Text>}

      <ActionButton
        label={form.isPending ? 'Enviando…' : 'Registrar pago'}
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
  summary: {
    color: colors.textPrimary,
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
