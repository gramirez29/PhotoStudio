import type {
  CreateBookingFormErrors,
  CreateBookingFormValues,
  CreateBookingTextField,
} from '../forms/createBookingForm.types';

/** State and actions of the create-booking form, as returned by `useNewBookingForm`. */
export interface NewBookingFormState {
  /** Today, the earliest day that can be picked. */
  readonly today: Date;
  /** Current values of the form. */
  readonly values: CreateBookingFormValues;
  /** Validation messages by field. */
  readonly errors: CreateBookingFormErrors;
  /** True while the booking is being created. */
  readonly isPending: boolean;
  /** Message for the last failed request, or null. */
  readonly submitError: string | null;
  /** Updates a typed field and clears its validation message. */
  readonly setText: (field: CreateBookingTextField, text: string) => void;
  /** Updates the client phone, formatted as 0000-0000, and clears its validation message. */
  readonly setPhone: (text: string) => void;
  /** Selects the package (service) and clears its validation message. */
  readonly setPackage: (packageName: string) => void;
  /** Updates the session start and clears its validation message. */
  readonly setStart: (start: Date) => void;
  /** Updates the session duration, in hours. */
  readonly setDuration: (hours: number) => void;
  /** Validates the form and, if it is valid, creates the booking and opens it. */
  readonly submit: () => void;
}
