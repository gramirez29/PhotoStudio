import type { CreateBookingRequest, PackageTier } from '../api/booking';
import type { SelectedExtra } from '../utils/pricing.types';

/** Values typed or picked in the create-booking form. */
export interface CreateBookingFormValues {
  /** Client name as typed. */
  readonly clientName: string;
  /** Client phone as typed. */
  readonly clientPhone: string;
  /** Client email as typed; optional. */
  readonly clientEmail: string;
  /** Type of session picked (family portrait, pre-wedding...); the backend stores it as the package name. */
  readonly packageName: string;
  /** Package picked, or null while none is. */
  readonly packageTier: PackageTier | null;
  /** Price of the package as typed, in colones. */
  readonly price: string;
  /** Extras chosen for the booking. */
  readonly extras: readonly SelectedExtra[];
  /** Discount as typed, in colones; empty for none. */
  readonly discount: string;
  /** Session start, in the device time zone. */
  readonly start: Date;
  /** Session length in hours. */
  readonly durationHours: number;
}

/** Form fields that can show a validation error. */
export type CreateBookingField =
  | 'clientName'
  | 'clientPhone'
  | 'clientEmail'
  | 'packageName'
  | 'packageTier'
  | 'price'
  | 'discount'
  | 'start';

/** Validation messages by field; a missing key means the field is valid. */
export type CreateBookingFormErrors = Partial<Record<CreateBookingField, string>>;

/** Result of validating the form: the request to send, or the errors to show. */
export type CreateBookingFormResult =
  | { readonly ok: true; readonly request: CreateBookingRequest }
  | { readonly ok: false; readonly errors: CreateBookingFormErrors };

/** Form fields edited by typing text. */
export type CreateBookingTextField = 'clientName' | 'clientEmail' | 'price' | 'discount';
