import type { ReactElement } from 'react';
import { ActivityIndicator, KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, Text } from 'react-native';
import { SERVICES } from '../constants/services';
import { DURATION_OPTIONS_HOURS } from '../forms/createBookingForm';
import { useNewBookingForm } from '../hooks/useNewBookingForm';
import { colors, radius, spacing } from '../theme/tokens';
import { DateTimeField } from './DateTimeField';
import { DurationPicker } from './DurationPicker';
import { FormInput } from './FormInput';
import { SelectField } from './SelectField';

/**
 * Form to create a booking in a few taps: client, package, price, day, time and duration.
 * @returns The form.
 */
export function NewBookingForm(): ReactElement {
  const form = useNewBookingForm();

  return (
    <KeyboardAvoidingView style={styles.flex} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
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
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

/** Styles of the form. */
const styles = StyleSheet.create({
  flex: {
    flex: 1,
  },
  container: {
    gap: spacing.md,
    padding: spacing.md,
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
