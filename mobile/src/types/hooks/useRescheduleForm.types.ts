/** State and actions of the reschedule form, as returned by `useRescheduleForm`. */
export interface RescheduleFormState {
  /** Today, the earliest day that can be picked. */
  readonly today: Date;
  /** New session start chosen by the photographer. */
  readonly newStart: Date;
  /** Validation message for the chosen date, or null. */
  readonly validationError: string | null;
  /** Message for the last failed request, or null. */
  readonly submitError: string | null;
  /** True while the booking is being moved. */
  readonly isPending: boolean;
  /** Updates the new start and clears the validation message. */
  readonly setNewStart: (next: Date) => void;
  /** Validates the new date and, if it is valid, moves the booking and goes back. */
  readonly submit: () => void;
}
