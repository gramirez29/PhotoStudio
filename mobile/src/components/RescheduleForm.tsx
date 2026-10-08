import type { ReactElement } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useRescheduleForm } from '../hooks/useRescheduleForm';
import { colors, radius, spacing } from '../theme/tokens';
import type { RescheduleFormProps } from '../types/components/RescheduleForm.types';
import { formatDateTime } from '../utils/format';
import { DateTimeField } from './DateTimeField';

/**
 * Form shown once the booking is loaded: the current date, a picker for the new one and the confirm button.
 * @param props Component props.
 * @returns The form.
 */
export function RescheduleForm({ booking }: RescheduleFormProps): ReactElement {
  const form = useRescheduleForm(booking);

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
        value={form.newStart}
        minimumDate={form.today}
        error={form.validationError ?? undefined}
        onChange={form.setNewStart}
      />
      <Text style={styles.hint}>La duración de la sesión se mantiene.</Text>

      {form.submitError !== null && <Text style={styles.submitError}>{form.submitError}</Text>}

      <Pressable
        accessibilityRole="button"
        disabled={form.isPending}
        onPress={form.submit}
        style={({ pressed }) => [styles.submit, form.isPending && styles.submitDisabled, pressed && styles.submitPressed]}
      >
        {form.isPending ? (
          <ActivityIndicator color={colors.textInverse} accessibilityLabel="Reprogramando" />
        ) : (
          <Text style={styles.submitLabel}>Reprogramar</Text>
        )}
      </Pressable>
    </ScrollView>
  );
}

/** Styles of the form. */
const styles = StyleSheet.create({
  container: {
    gap: spacing.md,
    padding: spacing.md,
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
