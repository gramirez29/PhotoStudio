import type { EditBookingDetailsRequest } from '../api/booking';

/** What the photographer typed and chose when correcting a booking. */
export interface EditBookingFormValues {
  /** Name of the client. */
  readonly clientName: string;
  /** Phone of the client, as typed (`7018-9220`). */
  readonly clientPhone: string;
  /** Email of the client; empty for none. */
  readonly clientEmail: string;
  /** Name of the package. Only used while the package can still change. */
  readonly packageName: string;
  /** Price of the package in colones, as typed. Only used while the package can still change. */
  readonly price: string;
}

/** Form fields that can show a validation message. */
export type EditBookingField = keyof EditBookingFormValues;

/** Fields the photographer types into a text box. */
export type EditBookingTextField = 'clientName' | 'clientEmail' | 'price';

/** Validation messages of the edit form, by field. */
export type EditBookingFormErrors = Partial<Record<EditBookingField, string>>;

/** Result of validating the edit form: the request to send, or the messages to show. */
export type EditBookingFormResult =
  | { readonly ok: true; readonly request: EditBookingDetailsRequest }
  | { readonly ok: false; readonly errors: EditBookingFormErrors };
