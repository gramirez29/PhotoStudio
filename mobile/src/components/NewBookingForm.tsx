import type { ReactElement } from 'react';
import { ActivityIndicator, Pressable, Text } from 'react-native';
import { SERVICES } from '../constants/services';
import { DURATION_OPTIONS_HOURS } from '../forms/createBookingForm';
import { useNewBookingForm } from '../hooks/useNewBookingForm';
import { radius, spacing } from '../theme/tokens';
import { DateTimeField } from './DateTimeField';
import { DurationPicker } from './DurationPicker';
import { ExtrasFields } from './ExtrasFields';
import { FormInput } from './FormInput';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';
import { PackageFields } from './PackageFields';
import { SelectField } from './SelectField';
import { TotalToPay } from './TotalToPay';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { useAppTheme } from '../hooks/useAppTheme';

/**
 * Form to create a booking in a few taps: client, type of session, package, price, day, time, duration, extras and discount. The
 * total to pay is shown before the button that creates the booking.
 * @returns The form.
 */
export function NewBookingForm(): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const form = useNewBookingForm();

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
        <PackageFields
          packageTier={form.values.packageTier}
          price={form.values.price}
          packageError={form.errors.packageTier}
          priceError={form.errors.price}
          onPackageChange={form.setPackageTier}
          onPriceChange={(text) => form.setText('price', text)}
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

        <ExtrasFields
          extras={form.values.extras}
          extrasTotal={form.totals.extrasTotal}
          discount={form.values.discount}
          discountError={form.errors.discount}
          onExtrasChange={form.setExtras}
          onDiscountChange={(text) => form.setText('discount', text)}
        />

        <TotalToPay total={form.totals.total} />

        {form.submitError !== null && <Text style={styles.submitError}>{form.submitError}</Text>}

        <Pressable
          accessibilityRole="button"
          disabled={form.isPending}
          onPress={form.submit}
          style={({ pressed }) => [styles.submit, form.isPending && styles.submitDisabled, pressed && styles.submitPressed]}
        >
          {form.isPending ? (
            <ActivityIndicator color={palette.onPrimary} accessibilityLabel="Creando reserva" />
          ) : (
            <Text style={styles.submitLabel}>Crear reserva</Text>
          )}
        </Pressable>
    </KeyboardAwareScreen>
  );
}

/** Styles of the form. */
const useStyles = makeStyles((palette) => ({
  hint: {
    color: palette.onSurfaceVariant,
    fontSize: 13,
    marginTop: -spacing.sm,
    fontFamily: FONT_FAMILIES.body,
  },
  submitError: {
    color: palette.error,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
  submit: {
    alignItems: 'center',
    backgroundColor: palette.primary,
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
    color: palette.onPrimary,
    fontSize: 16,
    fontFamily: FONT_FAMILIES.bodyMedium,
  },
}));
