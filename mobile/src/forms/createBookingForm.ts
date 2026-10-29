import type {
  CreateBookingField,
  CreateBookingFormResult,
  CreateBookingFormValues,
} from '../types/forms/createBookingForm.types';
import { addHours, nextDayAt } from '../utils/date';
import { normalizeEmail } from '../utils/email';
import { normalizePhone } from '../utils/phone';
import { parseOptionalAmount, parsePrice } from '../utils/price';
import { extrasTotal } from '../utils/pricing';

/** Currency used for every booking until the photographer can configure one. */
export const DEFAULT_CURRENCY = 'CRC';

/** Session durations offered in the form, in hours. */
export const DURATION_OPTIONS_HOURS = [1, 2, 3, 4] as const;

/** Hour of the day (local time) proposed for the session start. */
export const DEFAULT_START_HOUR = 9;

/** Duration proposed for a new session, in hours. */
export const DEFAULT_DURATION_HOURS = 2;

/**
 * Proposes the first values of the form: tomorrow at {@link DEFAULT_START_HOUR} and the default duration.
 * @param now Current instant.
 * @returns The initial values.
 */
export function initialFormValues(now: Date): CreateBookingFormValues {
  return {
    clientName: '',
    clientPhone: '',
    clientEmail: '',
    packageName: '',
    packageTier: null,
    price: '',
    extras: [],
    discount: '',
    start: nextDayAt(now, DEFAULT_START_HOUR),
    durationHours: DEFAULT_DURATION_HOURS,
  };
}

/**
 * Validates the form and, when it is valid, builds the request for the API.
 * @param values Values of the form.
 * @param now Current instant; the session must start after it.
 * @returns The request, or the validation errors by field.
 */
export function validateCreateBookingForm(
  values: CreateBookingFormValues,
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

  // The email is optional, but when it is typed it must be valid: it is where the client receives the verification codes.
  const typedEmail = values.clientEmail.trim();
  const clientEmail = typedEmail.length === 0 ? null : normalizeEmail(typedEmail);
  if (typedEmail.length > 0 && clientEmail === null) {
    errors.clientEmail = 'Escribe un correo válido, por ejemplo nombre@correo.com, o déjalo vacío.';
  }

  const packageName = values.packageName.trim();
  if (packageName.length === 0) {
    errors.packageName = 'Elige un tipo de sesión.';
  }

  if (values.packageTier === null) {
    errors.packageTier = 'Elige un paquete.';
  }

  const packagePrice = parsePrice(values.price);
  if (packagePrice === null) {
    errors.price = 'Escribe un precio mayor a cero.';
  }

  // The discount is optional, but it cannot be more than the client would pay without it.
  const discount = parseOptionalAmount(values.discount);
  if (discount === null) {
    errors.discount = 'Escribe un descuento válido o déjalo vacío.';
  } else if (packagePrice !== null && discount > packagePrice + extrasTotal(values.extras)) {
    errors.discount = 'El descuento no puede ser mayor al precio del paquete y los extras.';
  }

  if (values.start.getTime() <= now.getTime()) {
    errors.start = 'La sesión debe empezar en el futuro.';
  }

  if (clientPhone === null || packagePrice === null || discount === null || values.packageTier === null || Object.keys(errors).length > 0) {
    return { ok: false, errors };
  }

  return {
    ok: true,
    request: {
      clientName,
      clientPhone,
      ...(clientEmail === null ? {} : { clientEmail }),
      packageName,
      packagePrice,
      currency: DEFAULT_CURRENCY,
      sessionStart: values.start.toISOString(),
      sessionEnd: addHours(values.start, values.durationHours).toISOString(),
      packageTier: values.packageTier,
      ...(values.extras.length === 0
        ? {}
        : { extras: values.extras.map((extra) => ({ code: extra.code, name: extra.name, unitPrice: extra.unitPrice, quantity: extra.quantity })) }),
      ...(discount > 0 ? { discount } : {}),
    },
  };
}
