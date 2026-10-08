import type { APP_SUPPORTED_ACTIONS, REASON_ACTIONS } from '../../constants/bookingActions';
import type { BookingAction } from '../api/booking';

/** Visual weight of an action button. */
export type ActionVariant = 'primary' | 'danger';

/** Actions that open a screen to type a reason. */
export type ReasonAction = (typeof REASON_ACTIONS)[number];

/** The allowed actions of a booking, split by whether the app can perform them. */
export interface SplitActions {
  /** Actions the app can perform, in the order the app lists them. */
  readonly supported: readonly SupportedAction[];
  /** Allowed actions the app cannot perform yet. */
  readonly pending: readonly BookingAction[];
}

/** Texts of the confirmation dialog shown before an action that is not undone with a second screen. */
export interface ActionConfirmation {
  /** Title of the dialog. */
  readonly title: string;
  /** Question shown to the photographer. */
  readonly message: string;
  /** Label of the button that confirms. */
  readonly confirmLabel: string;
}

/** Texts of the reason form of an action. */
export interface ReasonFormCopy {
  /** Label of the reason field. */
  readonly label: string;
  /** Placeholder of the reason field. */
  readonly placeholder: string;
  /** Explanation shown under the field. */
  readonly hint: string;
  /** Label of the submit button. */
  readonly submitLabel: string;
}

/** An action the app can perform today. */
export type SupportedAction = (typeof APP_SUPPORTED_ACTIONS)[number];
