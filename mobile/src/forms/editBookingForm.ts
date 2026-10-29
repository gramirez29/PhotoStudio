import type { BookingResponse } from '../types/api/booking';
import type { EditBookingFormErrors, EditBookingFormResult, EditBookingFormValues } from '../types/forms/editBookingForm.types';
import { normalizeEmail } from '../utils/email';
import { formatStoredPhone, normalizePhone } from '../utils/phone';
import { parsePrice } from '../utils/price';

/**
 * Builds the form with what the booking has now, so the photographer only changes what is wrong.
 * @param booking Booking being corrected.
 * @returns The initial values.
 */
export function initialEditValues(booking: BookingResponse): EditBookingFormValues {
  return {
    clientName: booking.clientName,
    clientPhone: formatStoredPhone(booking.clientPhone),
    clientEmail: booking.clientEmail ?? '',
    packageName: booking.packageName,
    price: String(Math.round(booking.packagePrice.amount)),
  };
}

/**
 * Validates the edit form and, when it is valid, builds the request. The backend enforces the same rules; checking them here gives
 * a message next to the field without a round trip. When the package can no longer change, its values are taken from the booking
 * and not from the form, so a package the photographer cannot edit is never sent as a change.
 * @param values What the photographer typed and chose.
 * @param booking Booking being corrected.
 * @param packageEditable Whether the package and the price can still change.
 * @returns The request, or the validation messages by field.
 */
export function validateEditBookingForm(values: EditBookingFormValues, booking: BookingResponse, packageEditable: boolean): EditBookingFormResult {
  const errors: { -readonly [Field in keyof EditBookingFormValues]?: string } = {};

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

  let packageName = booking.packageName;
  let packagePrice = booking.packagePrice.amount;
  if (packageEditable) {
    packageName = values.packageName.trim();
    if (packageName.length === 0) {
      errors.packageName = 'Elige un paquete.';
    }

    const typedPrice = parsePrice(values.price);
    if (typedPrice === null) {
      errors.price = 'Escribe un precio mayor a cero.';
    } else {
      packagePrice = typedPrice;
    }
  }

  const errorsFound: EditBookingFormErrors = errors;
  if (clientPhone === null || Object.keys(errorsFound).length > 0) {
    return { ok: false, errors: errorsFound };
  }

  return {
    ok: true,
    request: {
      clientName,
      clientPhone,
      ...(clientEmail === null ? {} : { clientEmail }),
      packageName,
      packagePrice,
    },
  };
}
