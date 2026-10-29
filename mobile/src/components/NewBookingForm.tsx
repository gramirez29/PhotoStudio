import { useState, type ReactElement } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text, View } from 'react-native';
import { SERVICES } from '../constants/services';
import { DURATION_OPTIONS_HOURS } from '../forms/createBookingForm';
import { useNewBookingForm } from '../hooks/useNewBookingForm';
import { colors, radius, spacing } from '../theme/tokens';
import { formatColones, PACKAGE_LABELS, packageLabel } from '../utils/pricing';
import { DateTimeField } from './DateTimeField';
import { DurationPicker } from './DurationPicker';
import { ExtrasModal } from './ExtrasModal';
import { FormInput } from './FormInput';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';
import { SelectField } from './SelectField';

/**
 * Form to create a booking in a few taps: client, type of session, package, price, day, time, duration, extras and discount. The
 * total to pay is shown before the button that creates the booking.
 * @returns The form.
 */
export function NewBookingForm(): ReactElement {
  const form = useNewBookingForm();
  const [extrasOpen, setExtrasOpen] = useState(false);

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
          Con el correo, el cliente podrá confirmar y subir su comprobante desde el enlace. Sin él solo podrá ver la reserva.
        </Text>
        <SelectField
          label="Tipo de sesión"
          options={SERVICES}
          value={form.values.packageName}
          onChange={form.setSessionType}
          placeholder="Selecciona un tipo de sesión"
          error={form.errors.packageName}
        />
        <SelectField
          label="Paquete"
          options={PACKAGE_LABELS}
          value={packageLabel(form.values.packageTier)}
          onChange={form.setPackageTier}
          placeholder="Selecciona un paquete"
          error={form.errors.packageTier}
        />
        <FormInput
          label="Precio (₡)"
          value={form.values.price}
          onChangeText={(text) => form.setText('price', text)}
          error={form.errors.price}
          keyboardType="number-pad"
          placeholder="80000"
        />

        <DateTimeField
          label="Fecha y hora de la sesión"
          value={form.values.start}
          minimumDate={form.today}
          error={form.errors.start}
          onChange={form.setStart}
        />
        <DurationPicker
          label="Duración"
          options={DURATION_OPTIONS_HOURS}
          value={form.values.durationHours}
          onChange={form.setDuration}
        />

        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Agregar Extras"
          onPress={() => setExtrasOpen(true)}
          style={({ pressed }) => [styles.extrasButton, pressed && styles.submitPressed]}
        >
          <Text style={styles.extrasButtonLabel}>Agregar Extras</Text>
        </Pressable>
        <FormInput
          label="Extras (₡)"
          value={form.values.extras.length === 0 ? '' : formatColones(form.totals.extrasTotal)}
          editable={false}
          placeholder="Sin extras"
        />
        <FormInput
          label="Descuento (₡), opcional"
          value={form.values.discount}
          onChangeText={(text) => form.setText('discount', text)}
          error={form.errors.discount}
          keyboardType="number-pad"
          placeholder="0"
        />

        <View style={styles.total}>
          <Text style={styles.totalLabel}>Total a pagar</Text>
          <Text accessibilityLabel="Total a pagar" style={styles.totalValue}>
            {formatColones(form.totals.total)}
          </Text>
        </View>

        {form.submitError !== null && <Text style={styles.submitError}>{form.submitError}</Text>}

        <Pressable
          accessibilityRole="button"
          disabled={form.isPending}
          onPress={form.submit}
          style={({ pressed }) => [styles.submit, form.isPending && styles.submitDisabled, pressed && styles.submitPressed]}
        >
          {form.isPending ? (
            <ActivityIndicator color={colors.textInverse} accessibilityLabel="Creando reserva" />
          ) : (
            <Text style={styles.submitLabel}>Crear reserva</Text>
          )}
        </Pressable>

        {extrasOpen && (
          <ExtrasModal
            selected={form.values.extras}
            onConfirm={(extras) => {
              form.setExtras(extras);
              setExtrasOpen(false);
            }}
            onClose={() => setExtrasOpen(false)}
          />
        )}
    </KeyboardAwareScreen>
  );
}

/** Styles of the form. */
const styles = StyleSheet.create({
  hint: {
    color: colors.textSecondary,
    fontSize: 13,
    marginTop: -spacing.sm,
  },
  extrasButton: {
    alignItems: 'center',
    borderColor: colors.primary,
    borderRadius: radius.sm,
    borderWidth: 1,
    paddingVertical: spacing.md,
  },
  extrasButtonLabel: {
    color: colors.primary,
    fontSize: 16,
    fontWeight: '600',
  },
  total: {
    alignItems: 'center',
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: spacing.sm,
  },
  totalLabel: {
    color: colors.textPrimary,
    fontSize: 22,
    fontWeight: '700',
  },
  totalValue: {
    color: colors.textPrimary,
    fontSize: 26,
    fontWeight: '700',
  },
  submitError: {
    color: colors.danger,
    fontSize: 14,
  },
  submit: {
    alignItems: 'center',
    backgroundColor: colors.primary,
    borderRadius: radius.sm,
    paddingVertical: spacing.md,
  },
  submitDisabled: {
    opacity: 0.6,
  },
  submitPressed: {
    opacity: 0.8,
  },
  submitLabel: {
    color: colors.textInverse,
    fontSize: 16,
    fontWeight: '600',
  },
});
