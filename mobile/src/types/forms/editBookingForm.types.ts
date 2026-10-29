import type { EditBookingDetailsRequest, PackageTier } from '../api/booking';
import type { SelectedExtra } from '../utils/pricing.types';

/** What the photographer typed and chose when correcting a booking. */
export interface EditBookingFormValues {
  /** Name of the client. */
  readonly clientName: string;
  /** Phone of the client, as typed (`7018-9220`). */
  readonly clientPhone: string;
  /** Email of the client; empty for none. */
  readonly clientEmail: string;
  /** Type of session. Only used while the package can still change. */
  readonly packageName: string;
  /** Package the client booked, or null for a booking made before it was recorded. Only used while the package can still change. */
  readonly packageTier: PackageTier | null;
  /** Price of the package in colones, as typed. Only used while the package can still change. */
  readonly price: string;
  /** Extras of the booking. Only used while the package can still change. */
  readonly extras: readonly SelectedExtra[];
  /** Discount in colones, as typed; empty for none. Only used while the package can still change. */
  readonly discount: string;
}

/** Form fields that can show a validation message. */
export type EditBookingField = keyof EditBookingFormValues;

/** Fields the photographer types into a text box. */
export type EditBookingTextField = 'clientName' | 'clientEmail' | 'price' | 'discount';

/** Validation messages of the edit form, by field. */
export type EditBookingFormErrors = Partial<Record<EditBookingField, string>>;

/** Result of validating the edit form: the request to send, or the messages to show. */
export type EditBookingFormResult =
  | { readonly ok: true; readonly request: EditBookingDetailsRequest }
  | { readonly ok: false; readonly errors: EditBookingFormErrors };
