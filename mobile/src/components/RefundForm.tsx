import type { ReactElement } from 'react';
import { StyleSheet, Text } from 'react-native';
import { useRefundForm } from '../hooks/useRefundForm';
import { colors } from '../theme/tokens';
import type { RefundFormProps } from '../types/components/RefundForm.types';
import { MAX_REFUND_NOTE_LENGTH } from '../constants/billing';
import { formatMoney } from '../utils/format';
import { IN_PERSON_PAYMENT_LABELS, PAYMENT_METHOD_LABELS, inPersonMethodFromLabel } from '../utils/payments';
import { ActionButton } from './ActionButton';
import { FormInput } from './FormInput';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';
import { SelectField } from './SelectField';

/**
 * Form shown once the settlement is loaded: how much goes back and to whom, how it was given back, an optional note, and the
 * button that records it. Recording is final: the refund cannot be edited afterwards.
 * @param props Component props.
 * @returns The form.
 */
export function RefundForm({ settlement }: RefundFormProps): ReactElement {
  const form = useRefundForm(settlement.bookingId);

  return (
    <KeyboardAwareScreen align="top">
      <Text style={styles.title}>{settlement.clientName}</Text>
      <Text style={styles.subtitle}>{settlement.packageName}</Text>
      <Text style={styles.amount}>Devolver {formatMoney(settlement.refundAmount)}</Text>

      <SelectField
        label="¿Cómo lo devolviste?"
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
      <FormInput
        label="Nota (opcional)"
        value={form.note}
        onChangeText={form.setNote}
        error={form.validationError ?? undefined}
        placeholder="Por ejemplo: referencia del SINPE"
        maxLength={MAX_REFUND_NOTE_LENGTH}
        autoCapitalize="sentences"
      />
      <Text style={styles.hint}>
        Marca el reembolso solo cuando ya hiciste la transferencia o entregaste el efectivo. Un reembolso registrado no se
        puede editar.
      </Text>

      {form.submitError !== null && <Text style={styles.submitError}>{form.submitError}</Text>}

      <ActionButton
        label={form.isPending ? 'Enviando…' : 'Marcar como devuelto'}
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
  amount: {
    color: colors.textPrimary,
    fontSize: 18,
    fontWeight: '700',
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
