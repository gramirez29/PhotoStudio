import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import type { EditBookingFormValues } from '../../types/forms/editBookingForm.types';
import { initialEditValues, validateEditBookingForm } from '../editBookingForm';

const booking = parseBooking({ ...bookingPayload, clientPhone: '+50670189220', clientEmail: 'maria@example.com' });

/** A form that is valid as it is. */
const VALID: EditBookingFormValues = {
  clientName: '  María Pérez Solano  ',
  clientPhone: '7018-9221',
  clientEmail: '  Nuevo@Example.com ',
  packageName: ' Pre-Boda ',
  price: '150000',
};

describe('initialEditValues', () => {
  it('starts with what the booking has now, the phone written as it is typed', () => {
    expect(initialEditValues(booking)).toEqual({
      clientName: booking.clientName,
      clientPhone: '7018-9220',
      clientEmail: 'maria@example.com',
      packageName: booking.packageName,
      price: '100000',
    });
  });

  it('leaves the email empty when the booking has none', () => {
    expect(initialEditValues(parseBooking({ ...bookingPayload, clientEmail: null })).clientEmail).toBe('');
  });
});

describe('validateEditBookingForm', () => {
  it('builds the request with the normalized phone and email and the new package', () => {
    expect(validateEditBookingForm(VALID, booking, true)).toEqual({
      ok: true,
      request: {
        clientName: 'María Pérez Solano',
        clientPhone: '+50670189221',
        clientEmail: 'nuevo@example.com',
        packageName: 'Pre-Boda',
        packagePrice: 150000,
      },
    });
  });

  it('leaves the email out when it is empty, which removes it', () => {
    const result = validateEditBookingForm({ ...VALID, clientEmail: '   ' }, booking, true);

    expect(result.ok && 'clientEmail' in result.request).toBe(false);
  });

  it('takes the package and the price from the booking when they can no longer change, whatever the form says', () => {
    const result = validateEditBookingForm({ ...VALID, packageName: 'Otro', price: '1' }, booking, false);

    expect(result).toMatchObject({ ok: true, request: { packageName: booking.packageName, packagePrice: booking.packagePrice.amount } });
  });

  it('does not ask about the package when it cannot change, even if the form has it empty', () => {
    expect(validateEditBookingForm({ ...VALID, packageName: '', price: '' }, booking, false).ok).toBe(true);
  });

  it('asks for what is missing, each next to its field', () => {
    const result = validateEditBookingForm({ clientName: ' ', clientPhone: '12', clientEmail: 'mal', packageName: '', price: '0' }, booking, true);

    expect(!result.ok && Object.keys(result.errors).sort()).toEqual(['clientEmail', 'clientName', 'clientPhone', 'packageName', 'price']);
  });

  it('accepts a phone with the country code', () => {
    expect(validateEditBookingForm({ ...VALID, clientPhone: '+506 7018-9221' }, booking, true)).toMatchObject({
      ok: true,
      request: { clientPhone: '+50670189221' },
    });
  });
});
