import type { CreateBookingRequest } from '../api/booking';

/** Values typed or picked in the create-booking form. */
export interface CreateBookingFormValues {
  /** Client name as typed. */
  readonly clientName: string;
  /** Client phone as typed. */
  readonly clientPhone: string;
  /** Package name as typed. */
  readonly packageName: string;
  /** Price as typed, in colones. */
  readonly price: string;
  /** Session start, in the device time zone. */
  readonly start: Date;
  /** Session length in hours. */
  readonly durationHours: number;
}

/** Form fields that can show a validation error. */
export type CreateBookingField = 'clientName' | 'clientPhone' | 'packageName' | 'price' | 'start';

/** Validation messages by field; a missing key means the field is valid. */
export type CreateBookingFormErrors = Partial<Record<CreateBookingField, string>>;

/** Result of validating the form: the request to send, or the errors to show. */
export type CreateBookingFormResult =
  | { readonly ok: true; readonly request: CreateBookingRequest }
  | { readonly ok: false; readonly errors: CreateBookingFormErrors };

/** Form fields edited by typing text. */
export type CreateBookingTextField = 'clientName' | 'clientPhone' | 'price';
