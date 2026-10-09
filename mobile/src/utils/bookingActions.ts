import { APP_SUPPORTED_ACTIONS, REASON_ACTIONS } from '../constants/bookingActions';
import type { BookingAction } from '../types/api/booking';
import type {
  ActionConfirmation,
  ActionVariant,
  ReasonAction,
  ReasonFormCopy,
  SplitActions,
  SupportedAction,
} from '../types/utils/bookingActions.types';

/** Visual weight of each action the app can perform: the ones that free the slot or end the booking are red. */
export const ACTION_VARIANTS: Readonly<Record<SupportedAction, ActionVariant>> = {
  SignContract: 'primary',
  RecordInPersonPayment: 'primary',
  Reschedule: 'primary',
  Complete: 'primary',
  RevertClientAbsent: 'primary',
  MarkClientAbsent: 'danger',
  Cancel: 'danger',
};

/**
 * Splits the allowed actions of a booking into those the app can perform and those it cannot yet.
 * @param allowed Actions the API allows right now.
 * @returns The supported actions, in the order the app lists them, and the pending ones.
 */
export function splitActions(allowed: readonly BookingAction[]): SplitActions {
  return {
    supported: APP_SUPPORTED_ACTIONS.filter((action) => allowed.includes(action)),
    pending: allowed.filter((action) => !APP_SUPPORTED_ACTIONS.some((supported) => supported === action)),
  };
}

/**
 * Builds the texts of the confirmation dialog for an action that is performed right away.
 * @param action Action to confirm.
 * @param clientName Name of the client of the booking.
 * @returns The dialog texts.
 */
export function confirmationFor(action: 'Complete' | 'MarkClientAbsent', clientName: string): ActionConfirmation {
  if (action === 'Complete') {
    return {
      title: 'Completar sesión',
      message: `¿Marcar la sesión de ${clientName} como completada?`,
      confirmLabel: 'Completar',
    };
  }

  return {
    title: 'Cliente ausente',
    message: `¿Marcar a ${clientName} como ausente? Se libera el horario de la sesión.`,
    confirmLabel: 'Marcar ausente',
  };
}

/**
 * Reads the action received as a route parameter.
 * @param raw Value of the parameter.
 * @returns The action, or null when the parameter is missing or is not an action that needs a reason.
 */
export function parseReasonAction(raw: string | string[] | undefined): ReasonAction | null {
  const value = Array.isArray(raw) ? raw[0] : raw;
  return REASON_ACTIONS.find((action) => action === value) ?? null;
}

/**
 * Builds the texts of the reason form of an action.
 * @param action Action that needs a reason.
 * @returns The texts.
 */
export function reasonFormCopy(action: ReasonAction): ReasonFormCopy {
  if (action === 'Cancel') {
    return {
      label: 'Motivo de la cancelación',
      placeholder: 'Por ejemplo: el cliente cambió de planes',
      hint: 'Es obligatorio en las reservas confirmadas. Al cancelar se libera el horario.',
      submitLabel: 'Cancelar reserva',
    };
  }

  return {
    label: 'Motivo de la reversión',
    placeholder: 'Por ejemplo: lo marqué ausente por error',
    hint: 'Es obligatorio. La reserva vuelve a confirmada y retoma su horario, si sigue libre.',
    submitLabel: 'Revertir ausencia',
  };
}
