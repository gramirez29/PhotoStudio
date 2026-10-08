import type { CreateBookingRequest } from '../api/types';

/** Currency used for every booking until the photographer can configure one. */
export const DEFAULT_CURRENCY = 'CRC';

/** Country code added to local phone numbers typed with 8 digits (Costa Rica). */
export const DEFAULT_COUNTRY_CODE = '+506';

/** Session durations offered in the form, in hours. */
export const DURATION_OPTIONS_HOURS = [1, 2, 3, 4] as const;

/** Hour of the day (local time) proposed for the session start. */
export const DEFAULT_START_HOUR = 9;

/** Duration proposed for a new session, in hours. */
export const DEFAULT_DURATION_HOURS = 2;

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

/**
 * Proposes the first values of the form: tomorrow at {@link DEFAULT_START_HOUR} and the default duration.
 * @param now Current instant.
 * @returns The initial values.
 */
export function initialFormValues(now: Date): CreateBookingFormValues {
  const start = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1, DEFAULT_START_HOUR, 0, 0, 0);
  return { clientName: '', clientPhone: '', packageName: '', price: '', start, durationHours: DEFAULT_DURATION_HOURS };
}

/**
 * Normalizes a phone number to international format. Eight local digits get {@link DEFAULT_COUNTRY_CODE}.
 * @param raw Phone as typed (spaces, dashes and parentheses are ignored).
 * @returns The number like `+50688881111`, or null when it cannot be a phone number.
 */
export function normalizePhone(raw: string): string | null {
  const trimmed = raw.trim();
  const digits = trimmed.replace(/\D/g, '');

  if (trimmed.startsWith('+')) {
    return digits.length >= 8 && digits.length <= 15 ? `+${digits}` : null;
  }

  if (digits.length === 8) {
    return `${DEFAULT_COUNTRY_CODE}${digits}`;
  }

  const codeDigits = DEFAULT_COUNTRY_CODE.slice(1);
  if (digits.length === 8 + codeDigits.length && digits.startsWith(codeDigits)) {
    return `+${digits}`;
  }

  return null;
}

/**
 * Parses a price typed in whole colones. Spaces, dots, commas and the colon sign are accepted as grouping marks.
 * @param raw Price as typed.
 * @returns The amount, or null when it is empty, not a number or not greater than zero.
 */
export function parsePrice(raw: string): number | null {
  const cleaned = raw.replace(/[\s.,₡]/g, '');
  if (!/^\d+$/.test(cleaned)) {
    return null;
  }

  const amount = Number(cleaned);
  return Number.isSafeInteger(amount) && amount > 0 ? amount : null;
}

/**
 * Replaces the calendar day of a date, keeping its time of day.
 * @param base Date whose time of day is kept.
 * @param day Date that provides the year, month and day.
 * @returns A new date.
 */
export function withDayOf(base: Date, day: Date): Date {
  return new Date(day.getFullYear(), day.getMonth(), day.getDate(), base.getHours(), base.getMinutes(), 0, 0);
}

/**
 * Replaces the time of day of a date, keeping its calendar day.
 * @param base Date whose day is kept.
 * @param time Date that provides the hours and minutes.
 * @returns A new date.
 */
export function withTimeOf(base: Date, time: Date): Date {
  return new Date(base.getFullYear(), base.getMonth(), base.getDate(), time.getHours(), time.getMinutes(), 0, 0);
}

/**
 * Validates the form and, when it is valid, builds the request for the API.
 * @param values Values of the form.
 * @param photographerId Photographer (tenant) identifier.
 * @param now Current instant; the session must start after it.
 * @returns The request, or the validation errors by field.
 */
export function validateCreateBookingForm(
  values: CreateBookingFormValues,
  photographerId: string,
  now: Date,
): CreateBookingFormResult {
  const errors: { -readonly [Field in CreateBookingField]?: string } = {};

  const clientName = values.clientName.trim();
  if (clientName.length === 0) {
    errors.clientName = 'Escribe el nombre del cliente.';
  }

  const clientPhone = normalizePhone(values.clientPhone);
  if (clientPhone === null) {
    errors.clientPhone = 'Escribe un teléfono válido (8 dígitos o con código de país).';
  }

  const packageName = values.packageName.trim();
  if (packageName.length === 0) {
    errors.packageName = 'Escribe el nombre del paquete.';
  }

  const packagePrice = parsePrice(values.price);
  if (packagePrice === null) {
    errors.price = 'Escribe un precio mayor a cero.';
  }

  if (values.start.getTime() <= now.getTime()) {
    errors.start = 'La sesión debe empezar en el futuro.';
  }

  if (clientPhone === null || packagePrice === null || Object.keys(errors).length > 0) {
    return { ok: false, errors };
  }

  const end = new Date(values.start.getTime() + values.durationHours * 60 * 60 * 1000);
  return {
    ok: true,
    request: {
      photographerId,
      clientName,
      clientPhone,
      packageName,
      packagePrice,
      currency: DEFAULT_CURRENCY,
      sessionStart: values.start.toISOString(),
      sessionEnd: end.toISOString(),
    },
  };
}
