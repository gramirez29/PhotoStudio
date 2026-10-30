import type { ReactElement } from 'react';
import { Text } from 'react-native';
import { useRefundForm } from '../hooks/useRefundForm';
import type { RefundFormProps } from '../types/components/RefundForm.types';
import { MAX_REFUND_NOTE_LENGTH } from '../constants/billing';
import { formatMoney } from '../utils/format';
import { IN_PERSON_PAYMENT_LABELS, PAYMENT_METHOD_LABELS, inPersonMethodFromLabel } from '../utils/payments';
import { ActionButton } from './ActionButton';
import { FormInput } from './FormInput';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';
import { SelectField } from './SelectField';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * Form shown once the settlement is loaded: how much goes back and to whom, how it was given back, an optional note, and the
 * button that records it. Recording is final: the refund cannot be edited afterwards.
 * @param props Component props.
 * @returns The form.
 */
export function RefundForm({ settlement }: RefundFormProps): ReactElement {
  const styles = useStyles();
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
  amount: {
    color: palette.onSurface,
    fontSize: 18,
    fontFamily: FONT_FAMILIES.titleBold,
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
