import type { ReactElement } from 'react';
import { StyleSheet, Text } from 'react-native';
import { SERVICES } from '../constants/services';
import { useEditBookingForm } from '../hooks/useEditBookingForm';
import { colors, spacing } from '../theme/tokens';
import type { EditBookingFormProps } from '../types/components/EditBookingForm.types';
import { formatMoney } from '../utils/format';
import { packageLabel } from '../utils/pricing';
import { ActionButton } from './ActionButton';
import { ExtrasFields } from './ExtrasFields';
import { FormInput } from './FormInput';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';
import { PackageFields } from './PackageFields';
import { SelectField } from './SelectField';
import { TotalToPay } from './TotalToPay';
import { AppCard } from './AppCard';

/**
 * Screen content that corrects a booking: the data of the client, and the type of session, package, price, extras and discount
 * while the backend still allows it. Once the contract is signed or a payment is recorded they are shown but cannot change,
 * because the contract text and the deposit are worked out from them.
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
            label="Tipo de sesión"
            options={SERVICES}
            value={form.values.packageName}
            onChange={form.setPackage}
            placeholder="Selecciona un tipo de sesión"
            error={form.errors.packageName}
          />
          <PackageFields
            packageTier={form.values.packageTier}
            price={form.values.price}
            packageError={form.errors.packageTier}
            priceError={form.errors.price}
            onPackageChange={form.setPackageTier}
            onPriceChange={(text) => form.setText('price', text)}
          />
          <ExtrasFields
            extras={form.values.extras}
            extrasTotal={form.totals.extrasTotal}
            discount={form.values.discount}
            discountError={form.errors.discount}
            onExtrasChange={form.setExtras}
            onDiscountChange={(text) => form.setText('discount', text)}
          />
          <TotalToPay total={form.totals.total} />
        </>
      ) : (
        <AppCard style={styles.locked}>
          <Text style={styles.lockedTitle}>Paquete, extras y precio</Text>
          <Text style={styles.lockedValue}>
            {[booking.packageName, packageLabel(booking.packageTier)].filter((part) => part.length > 0).join(' · ')}
          </Text>
          {booking.extras.map((extra) => (
            <Text key={extra.code} style={styles.lockedValue}>
              {extra.name} × {extra.quantity} · {formatMoney(extra.total)}
            </Text>
          ))}
          {booking.discount.amount > 0 && <Text style={styles.lockedValue}>Descuento · {formatMoney(booking.discount)}</Text>}
          <Text style={styles.lockedValue}>Total · {formatMoney(booking.packagePrice)}</Text>
          <Text style={styles.hint}>
            No se pueden cambiar porque el contrato ya está firmado o hay pagos registrados: el contrato y el anticipo salen de ellos.
          </Text>
        </AppCard>
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
    gap: spacing.xs,
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
