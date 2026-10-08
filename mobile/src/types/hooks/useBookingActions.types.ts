/** Handlers and state of the booking actions, as returned by `useBookingActions`. */
export interface BookingActionsState {
  /** True while an action is being sent. */
  readonly isPending: boolean;
  /** Message for the last failed action, or null. */
  readonly errorMessage: string | null;
  /** Opens the reschedule form. */
  readonly reschedule: () => void;
  /** Opens the form to type the reason for cancelling. */
  readonly cancel: () => void;
  /** Asks for confirmation and marks the session as completed. */
  readonly complete: () => void;
  /** Asks for confirmation and marks the client as absent. */
  readonly markClientAbsent: () => void;
  /** Opens the form to type the reason for reverting the absence. */
  readonly revertClientAbsent: () => void;
}
