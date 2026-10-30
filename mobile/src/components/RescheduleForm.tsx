import type { ReactElement } from 'react';
import { ActivityIndicator, Pressable, ScrollView, Text } from 'react-native';
import { useRescheduleForm } from '../hooks/useRescheduleForm';
import { radius, spacing } from '../theme/tokens';
import type { RescheduleFormProps } from '../types/components/RescheduleForm.types';
import { formatDateTime } from '../utils/format';
import { DateTimeField } from './DateTimeField';
import { AppCard } from './AppCard';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { useAppTheme } from '../hooks/useAppTheme';

/**
 * Form shown once the booking is loaded: the current date, a picker for the new one and the confirm button.
 * @param props Component props.
 * @returns The form.
 */
export function RescheduleForm({ booking }: RescheduleFormProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const form = useRescheduleForm(booking);

  return (
    <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
      <Text style={styles.title}>{booking.clientName}</Text>
      <Text style={styles.subtitle}>{booking.packageName}</Text>

      <AppCard style={styles.card}>
        <Text style={styles.cardLabel}>Fecha actual</Text>
        <Text style={styles.cardValue}>{formatDateTime(booking.sessionStart)}</Text>
      </AppCard>

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
          <ActivityIndicator color={palette.onPrimary} accessibilityLabel="Reprogramando" />
        ) : (
          <Text style={styles.submitLabel}>Reprogramar</Text>
        )}
      </Pressable>
    </ScrollView>
  );
}

/** Styles of the form. */
const useStyles = makeStyles((palette) => ({
  container: {
    gap: spacing.md,
    padding: spacing.md,
  },
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
  card: {
    gap: spacing.xs,
  },
  cardLabel: {
    color: palette.onSurfaceVariant,
    fontSize: 13,
    fontFamily: FONT_FAMILIES.body,
  },
  cardValue: {
    color: palette.onSurface,
    fontSize: 16,
    fontFamily: FONT_FAMILIES.bodyMedium,
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
