import { router } from 'expo-router';
import { useState, type ReactElement } from 'react';
import {
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { DateTimeField } from '../../components/DateTimeField';
import { FormInput } from '../../components/FormInput';
import { env } from '../../config/env';
import {
  DURATION_OPTIONS_HOURS,
  initialFormValues,
  validateCreateBookingForm,
  type CreateBookingField,
  type CreateBookingFormErrors,
  type CreateBookingFormValues,
} from '../../forms/createBookingForm';
import { useCreateBooking } from '../../hooks/useCreateBooking';
import { colors, radius, spacing } from '../../theme/tokens';
import { createBookingErrorMessage } from '../../utils/apiErrors';

/** Text fields of the form that are edited by typing. */
type TextField = 'clientName' | 'clientPhone' | 'packageName' | 'price';

/**
 * Form to create a booking in a few taps: client, package, price, day, time and duration.
 * @returns The screen.
 */
export default function NewBookingScreen(): ReactElement {
  const createBooking = useCreateBooking();
  const [today] = useState(() => new Date());
  const [values, setValues] = useState<CreateBookingFormValues>(() => initialFormValues(today));
  const [errors, setErrors] = useState<CreateBookingFormErrors>({});

  /**
   * Clears the validation message of a field once the photographer edits it.
   * @param field Field being edited.
   */
  function clearError(field: CreateBookingField): void {
    setErrors((previous) => {
      const next = { ...previous };
      delete next[field];
      return next;
    });
  }

  /**
   * Updates a typed field.
   * @param field Field being edited.
   * @param text New text.
   */
  function setText(field: TextField, text: string): void {
    setValues((previous) => ({ ...previous, [field]: text }));
    clearError(field);
  }

  /**
   * Validates the form and, if it is valid, sends it. Opens the new booking when the server accepts it.
   */
  function submit(): void {
    if (env.photographerId === null) {
      return;
    }

    const result = validateCreateBookingForm(values, env.photographerId, new Date());
    if (!result.ok) {
      setErrors(result.errors);
      return;
    }

    setErrors({});
    createBooking.mutate(result.request, {
      onSuccess: (booking) => router.replace({ pathname: '/bookings/[id]', params: { id: booking.id } }),
    });
  }

  if (env.photographerId === null) {
    return (
      <View style={styles.centered}>
        <Text style={styles.message}>Falta configurar EXPO_PUBLIC_PHOTOGRAPHER_ID en el archivo .env de la app.</Text>
      </View>
    );
  }

  return (
    <KeyboardAvoidingView style={styles.flex} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
        <FormInput
          label="Cliente"
          value={values.clientName}
          onChangeText={(text) => setText('clientName', text)}
          error={errors.clientName}
          autoCapitalize="words"
          autoCorrect={false}
          placeholder="Nombre del cliente"
        />
        <FormInput
          label="Teléfono"
          value={values.clientPhone}
          onChangeText={(text) => setText('clientPhone', text)}
          error={errors.clientPhone}
          keyboardType="phone-pad"
          textContentType="telephoneNumber"
          placeholder="8888 8888"
        />
        <FormInput
          label="Paquete"
          value={values.packageName}
          onChangeText={(text) => setText('packageName', text)}
          error={errors.packageName}
          autoCapitalize="sentences"
          placeholder="Retrato familiar"
        />
        <FormInput
          label="Precio (₡)"
          value={values.price}
          onChangeText={(text) => setText('price', text)}
          error={errors.price}
          keyboardType="number-pad"
          placeholder="80000"
        />

        <DateTimeField
          label="Fecha y hora de la sesión"
          value={values.start}
          minimumDate={today}
          error={errors.start}
          onChange={(start) => {
            setValues((previous) => ({ ...previous, start }));
            clearError('start');
          }}
        />

        <View style={styles.durationGroup}>
          <Text style={styles.label}>Duración</Text>
          <View style={styles.durationRow}>
            {DURATION_OPTIONS_HOURS.map((hours) => {
              const selected = values.durationHours === hours;
              return (
                <Pressable
                  key={hours}
                  accessibilityRole="button"
                  accessibilityState={{ selected }}
                  accessibilityLabel={`${hours} ${hours === 1 ? 'hora' : 'horas'}`}
                  onPress={() => setValues((previous) => ({ ...previous, durationHours: hours }))}
                  style={[styles.chip, selected && styles.chipSelected]}
                >
                  <Text style={[styles.chipText, selected && styles.chipTextSelected]}>{hours} h</Text>
                </Pressable>
              );
            })}
          </View>
        </View>

        {createBooking.isError && <Text style={styles.submitError}>{createBookingErrorMessage(createBooking.error)}</Text>}

        <Pressable
          accessibilityRole="button"
          disabled={createBooking.isPending}
          onPress={submit}
          style={({ pressed }) => [
            styles.submit,
            createBooking.isPending && styles.submitDisabled,
            pressed && styles.submitPressed,
          ]}
        >
          {createBooking.isPending ? (
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
  centered: {
    alignItems: 'center',
    flex: 1,
    justifyContent: 'center',
    padding: spacing.lg,
  },
  message: {
    color: colors.textSecondary,
    fontSize: 15,
    textAlign: 'center',
  },
  label: {
    color: colors.textPrimary,
    fontSize: 14,
    fontWeight: '600',
  },
  durationGroup: {
    gap: spacing.xs,
  },
  durationRow: {
    flexDirection: 'row',
    gap: spacing.sm,
  },
  chip: {
    alignItems: 'center',
    borderColor: colors.border,
    borderRadius: radius.pill,
    borderWidth: 1,
    flex: 1,
    paddingVertical: spacing.sm,
  },
  chipSelected: {
    backgroundColor: colors.primary,
    borderColor: colors.primary,
  },
  chipText: {
    color: colors.textPrimary,
    fontSize: 15,
    fontWeight: '600',
  },
  chipTextSelected: {
    color: colors.textInverse,
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
