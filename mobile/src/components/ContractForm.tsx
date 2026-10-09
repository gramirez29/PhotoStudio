import type { ReactElement } from 'react';
import { StyleSheet, Text } from 'react-native';
import { useContractForm } from '../hooks/useContractForm';
import { colors } from '../theme/tokens';
import type { ContractFormProps } from '../types/components/ContractForm.types';
import { formatDateTime } from '../utils/format';
import { CONTRACT_SIGNATURE_LABELS, signatureKindFromLabel } from '../utils/payments';
import { ActionButton } from './ActionButton';
import { FormInput } from './FormInput';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';
import { SelectField } from './SelectField';

/**
 * Form shown once the booking is loaded: who signs the contract, how, and the button that records the signature.
 * @param props Component props.
 * @returns The form.
 */
export function ContractForm({ booking }: ContractFormProps): ReactElement {
  const form = useContractForm(booking);

  return (
    <KeyboardAwareScreen align="top">
      <Text style={styles.title}>{booking.clientName}</Text>
      <Text style={styles.subtitle}>
        {booking.packageName} · {formatDateTime(booking.sessionStart)}
      </Text>

      <SelectField
        label="Tipo de firma"
        options={Object.values(CONTRACT_SIGNATURE_LABELS)}
        value={CONTRACT_SIGNATURE_LABELS[form.kind]}
        placeholder="Elige cómo se firma"
        onChange={(label) => {
          const kind = signatureKindFromLabel(label);
          if (kind !== null) {
            form.setKind(kind);
          }
        }}
      />
      <FormInput
        label={form.kind === 'Paper' ? 'Nombre que aparece en el contrato' : 'Nombre de quien firma'}
        value={form.signerName}
        onChangeText={form.setSignerName}
        error={form.validationError ?? undefined}
        autoCapitalize="words"
      />
      <Text style={styles.hint}>
        {form.kind === 'Paper'
          ? 'Toma una foto del contrato firmado y guárdala; aquí solo queda registrado que se firmó en papel.'
          : 'Entrega el celular al cliente para que escriba su nombre como firma.'}
      </Text>

      {form.submitError !== null && <Text style={styles.submitError}>{form.submitError}</Text>}

      <ActionButton
        label={form.isPending ? 'Enviando…' : 'Registrar firma'}
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
