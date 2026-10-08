import type { ReactElement } from 'react';
import { StyleSheet, Text } from 'react-native';
import { useReasonForm } from '../hooks/useReasonForm';
import { colors } from '../theme/tokens';
import type { ReasonFormProps } from '../types/components/ReasonForm.types';
import { ACTION_VARIANTS, reasonFormCopy } from '../utils/bookingActions';
import { formatDateTime } from '../utils/format';
import { ActionButton } from './ActionButton';
import { FormInput } from './FormInput';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';

/**
 * Form shown once the booking is loaded: which booking it is, the reason field and the button that performs the action.
 * @param props Component props.
 * @returns The form.
 */
export function ReasonForm({ booking, action }: ReasonFormProps): ReactElement {
  const form = useReasonForm(booking, action);
  const copy = reasonFormCopy(action);

  return (
    <KeyboardAwareScreen align="top">
      <Text style={styles.title}>{booking.clientName}</Text>
      <Text style={styles.subtitle}>
        {booking.packageName} · {formatDateTime(booking.sessionStart)}
      </Text>

      <FormInput
        label={copy.label}
        value={form.text}
        onChangeText={form.setText}
        error={form.validationError ?? undefined}
        placeholder={copy.placeholder}
        multiline
        numberOfLines={3}
        autoCapitalize="sentences"
      />
      <Text style={styles.hint}>{copy.hint}</Text>

      {form.submitError !== null && <Text style={styles.submitError}>{form.submitError}</Text>}

      <ActionButton
        label={form.isPending ? 'Enviando…' : copy.submitLabel}
        variant={ACTION_VARIANTS[action]}
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
