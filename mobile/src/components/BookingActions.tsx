import type { ReactElement } from 'react';
import { Text } from 'react-native';
import { useBookingActions } from '../hooks/useBookingActions';
import { spacing } from '../theme/tokens';
import type { BookingActionsProps } from '../types/components/BookingActions.types';
import type { SupportedAction } from '../types/utils/bookingActions.types';
import { ACTION_VARIANTS, splitActions } from '../utils/bookingActions';
import { BOOKING_ACTION_LABELS } from '../utils/labels';
import { ActionButton } from './ActionButton';
import { AppCard } from './AppCard';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * Card with the actions the photographer can perform on a booking right now. Only the actions the API allows are shown,
 * so the app never decides by itself what a status permits. Allowed actions the app cannot perform yet are listed apart.
 * @param props Component props.
 * @returns The card.
 */
export function BookingActions({ booking }: BookingActionsProps): ReactElement {
  const styles = useStyles();
  const actions = useBookingActions(booking);
  const { supported, pending } = splitActions(booking.allowedActions);

  const handlers: Readonly<Record<SupportedAction, () => void>> = {
    SignContract: actions.signContract,
    RecordInPersonPayment: actions.recordPayment,
    Reschedule: actions.reschedule,
    Complete: actions.complete,
    MarkClientAbsent: actions.markClientAbsent,
    RevertClientAbsent: actions.revertClientAbsent,
    EditDetails: actions.editDetails,
    Cancel: actions.cancel,
  };

  return (
    <AppCard style={styles.card}>
      <Text style={styles.title}>Acciones</Text>

      {supported.length === 0 && pending.length === 0 && (
        <Text style={styles.body}>No hay acciones disponibles en este estado.</Text>
      )}

      {supported.map((action) => (
        <ActionButton
          key={action}
          label={BOOKING_ACTION_LABELS[action]}
          variant={ACTION_VARIANTS[action]}
          disabled={actions.isPending}
          onPress={handlers[action]}
        />
      ))}

      {actions.errorMessage !== null && <Text style={styles.error}>{actions.errorMessage}</Text>}

      {pending.length > 0 && (
        <Text style={styles.hint}>
          Próximamente en la app: {pending.map((action) => BOOKING_ACTION_LABELS[action]).join(', ')}.
        </Text>
      )}
    </AppCard>
  );
}

/** Styles of the card. */
const useStyles = makeStyles((palette) => ({
  card: {
    gap: spacing.sm,
    marginTop: spacing.md,
  },
  title: {
    color: palette.onSurface,
    fontSize: 16,
    fontFamily: FONT_FAMILIES.bodyBold,
  },
  body: {
    color: palette.onSurface,
    fontSize: 15,
    fontFamily: FONT_FAMILIES.body,
  },
  error: {
    color: palette.error,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
  hint: {
    color: palette.onSurfaceVariant,
    fontSize: 13,
    fontFamily: FONT_FAMILIES.body,
  },
}));
