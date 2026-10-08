import { router, useLocalSearchParams } from 'expo-router';
import { useState, type ReactElement } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import type { BookingResponse } from '../../api/types';
import { DateTimeField } from '../../components/DateTimeField';
import { validateReschedule } from '../../forms/rescheduleBookingForm';
import { useBooking } from '../../hooks/useBooking';
import { useRescheduleBooking } from '../../hooks/useRescheduleBooking';
import { colors, radius, spacing } from '../../theme/tokens';
import { rescheduleBookingErrorMessage } from '../../utils/apiErrors';
import { formatDateTime } from '../../utils/format';

/** Props of {@link RescheduleForm}. */
interface RescheduleFormProps {
  /** Booking being moved. */
  readonly booking: BookingResponse;
}

/**
 * Form shown once the booking is loaded: the current date, a picker for the new one and the confirm button.
 * @param props Component props.
 * @returns The form.
 */
function RescheduleForm({ booking }: RescheduleFormProps): ReactElement {
  const reschedule = useRescheduleBooking(booking.id);
  const [today] = useState(() => new Date());
  const [newStart, setNewStart] = useState(() => new Date(booking.sessionStart));
  const [validationError, setValidationError] = useState<string | null>(null);

  /**
   * Validates the new date and, when it is valid, sends it. Goes back to the detail when the server accepts it.
   */
  function submit(): void {
    const result = validateReschedule(booking, newStart, new Date());
    if (!result.ok) {
      setValidationError(result.error);
      return;
    }

    setValidationError(null);
    reschedule.mutate(result.request, { onSuccess: () => router.back() });
  }

  return (
    <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
      <Text style={styles.title}>{booking.clientName}</Text>
      <Text style={styles.subtitle}>{booking.packageName}</Text>

      <View style={styles.card}>
        <Text style={styles.cardLabel}>Fecha actual</Text>
        <Text style={styles.cardValue}>{formatDateTime(booking.sessionStart)}</Text>
      </View>

      <DateTimeField
        label="Nueva fecha y hora"
        value={newStart}
        minimumDate={today}
        error={validationError ?? undefined}
        onChange={(next) => {
          setNewStart(next);
          setValidationError(null);
        }}
      />
      <Text style={styles.hint}>La duración de la sesión se mantiene.</Text>

      {reschedule.isError && <Text style={styles.submitError}>{rescheduleBookingErrorMessage(reschedule.error)}</Text>}

      <Pressable
        accessibilityRole="button"
        disabled={reschedule.isPending}
        onPress={submit}
        style={({ pressed }) => [
          styles.submit,
          reschedule.isPending && styles.submitDisabled,
          pressed && styles.submitPressed,
        ]}
      >
        {reschedule.isPending ? (
          <ActivityIndicator color={colors.textInverse} accessibilityLabel="Reprogramando" />
        ) : (
          <Text style={styles.submitLabel}>Reprogramar</Text>
        )}
      </Pressable>
    </ScrollView>
  );
}

/**
 * Screen to move a confirmed booking to another day or time. The booking comes from the cache of the detail screen.
 * @returns The screen.
 */
export default function RescheduleScreen(): ReactElement {
  const { id } = useLocalSearchParams<{ id: string }>();
  const bookingId = typeof id === 'string' ? id : '';
  const query = useBooking(bookingId);

  if (query.isPending) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator color={colors.primary} accessibilityLabel="Cargando reserva" />
      </View>
    );
  }

  if (query.isError) {
    return (
      <View style={styles.centered}>
        <Text style={styles.message}>No se pudo cargar la reserva. Revisa la conexión e inténtalo de nuevo.</Text>
      </View>
    );
  }

  return <RescheduleForm booking={query.data} />;
}

/** Styles of the screen. */
const styles = StyleSheet.create({
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
  title: {
    color: colors.textPrimary,
    fontSize: 22,
    fontWeight: '700',
  },
  subtitle: {
    color: colors.textSecondary,
    fontSize: 15,
  },
  card: {
    backgroundColor: colors.surface,
    borderRadius: radius.md,
    gap: spacing.xs,
    padding: spacing.md,
  },
  cardLabel: {
    color: colors.textSecondary,
    fontSize: 13,
  },
  cardValue: {
    color: colors.textPrimary,
    fontSize: 16,
    fontWeight: '600',
  },
  hint: {
    color: colors.textSecondary,
    fontSize: 13,
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
