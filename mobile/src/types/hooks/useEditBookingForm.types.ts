import type { EditBookingFormErrors, EditBookingFormValues, EditBookingTextField } from '../forms/editBookingForm.types';
import type { BookingTotals, SelectedExtra } from '../utils/pricing.types';

/** What `useEditBookingForm` gives the screen that corrects a booking. */
export interface EditBookingFormState {
  /** What was typed and chosen so far. */
  readonly values: EditBookingFormValues;
  /** Validation messages by field. */
  readonly errors: EditBookingFormErrors;
  /** Whether the changes are being sent. */
  readonly isPending: boolean;
  /** Message of the last failed save, or null. */
  readonly submitError: string | null;
  /** Whether the package and the price can still change; the backend says so with the action `EditPackage`. */
  readonly packageEditable: boolean;
  /** Sets the text of a field and clears its validation message. */
  readonly setText: (field: EditBookingTextField, text: string) => void;
  /** Sets the phone, written as it is typed (`7018-9220`). */
  readonly setPhone: (text: string) => void;
  /** What the booking costs with the package, extras and discount as they are in the form. */
  readonly totals: BookingTotals;
  /** Sets the type of session chosen from the list. */
  readonly setPackage: (packageName: string) => void;
  /** Sets the package by its name in the drop-down; fills in its price when it has one. */
  readonly setPackageTier: (label: string) => void;
  /** Replaces the extras chosen. */
  readonly setExtras: (extras: readonly SelectedExtra[]) => void;
  /** Validates the form and saves the changes. */
  readonly submit: () => void;
}
