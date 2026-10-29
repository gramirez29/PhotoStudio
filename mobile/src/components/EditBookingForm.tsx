import type { ReactElement } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { SERVICES } from '../constants/services';
import { useEditBookingForm } from '../hooks/useEditBookingForm';
import { colors, radius, spacing } from '../theme/tokens';
import type { EditBookingFormProps } from '../types/components/EditBookingForm.types';
import { formatMoney } from '../utils/format';
import { ActionButton } from './ActionButton';
import { FormInput } from './FormInput';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';
import { SelectField } from './SelectField';

/**
 * Screen content that corrects a booking: the data of the client, and the package and its price while the backend still allows
 * it. Once the contract is signed or a payment is recorded the package and the price are shown but cannot change, because the
 * contract text and the deposit are worked out from them.
 * @param props Component props.
 * @returns The form.
 */
export function EditBookingForm({ booking }: EditBookingFormProps): ReactElement {
  const form = useEditBookingForm(booking);

  return (
    <KeyboardAwareScreen align="top">
      <FormInput
        label="Cliente"
        value={form.values.clientName}
        onChangeText={(text) => form.setText('clientName', text)}
        error={form.errors.clientName}
        autoCapitalize="words"
        autoCorrect={false}
        placeholder="Nombre del cliente"
      />
      <FormInput
        label="Teléfono"
        value={form.values.clientPhone}
        onChangeText={form.setPhone}
        error={form.errors.clientPhone}
        keyboardType="phone-pad"
        textContentType="telephoneNumber"
        placeholder="8888-8888"
      />
      <FormInput
        label="Correo (opcional)"
        value={form.values.clientEmail}
        onChangeText={(text) => form.setText('clientEmail', text)}
        error={form.errors.clientEmail}
        keyboardType="email-address"
        autoCapitalize="none"
        autoCorrect={false}
        textContentType="emailAddress"
        placeholder="cliente@correo.com"
      />
      <Text style={styles.hint}>
        Con el correo, el cliente puede confirmar y subir su comprobante desde el enlace. Si lo cambias, el código que ya se había enviado al
        correo anterior deja de servir.
      </Text>

      {form.packageEditable ? (
        <>
          <SelectField
            label="Paquete"
            options={SERVICES}
            value={form.values.packageName}
            onChange={form.setPackage}
            placeholder="Selecciona un paquete"
            error={form.errors.packageName}
          />
          <FormInput
            label="Precio (₡)"
            value={form.values.price}
            onChangeText={(text) => form.setText('price', text)}
            error={form.errors.price}
            keyboardType="number-pad"
            placeholder="80000"
          />
        </>
      ) : (
        <View style={styles.locked}>
          <Text style={styles.lockedTitle}>Paquete y precio</Text>
          <Text style={styles.lockedValue}>
            {booking.packageName} · {formatMoney(booking.packagePrice)}
          </Text>
          <Text style={styles.hint}>
            No se pueden cambiar porque el contrato ya está firmado o hay pagos registrados: el contrato y el anticipo salen de ellos.
          </Text>
        </View>
      )}

      {form.submitError !== null && <Text style={styles.submitError}>{form.submitError}</Text>}

      <ActionButton label={form.isPending ? 'Guardando…' : 'Guardar cambios'} disabled={form.isPending} onPress={form.submit} />
    </KeyboardAwareScreen>
  );
}

/** Styles of the form. */
const styles = StyleSheet.create({
  hint: {
    color: colors.textSecondary,
    fontSize: 13,
  },
  locked: {
    backgroundColor: colors.surface,
    borderRadius: radius.md,
    gap: spacing.xs,
    padding: spacing.md,
  },
  lockedTitle: {
    color: colors.textPrimary,
    fontSize: 14,
    fontWeight: '600',
  },
  lockedValue: {
    color: colors.textPrimary,
    fontSize: 15,
  },
  submitError: {
    color: colors.danger,
    fontSize: 14,
  },
});
