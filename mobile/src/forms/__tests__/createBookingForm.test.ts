import type { CreateBookingFormValues } from '../../types/forms/createBookingForm.types';
import { DEFAULT_START_HOUR, initialFormValues, validateCreateBookingForm } from '../createBookingForm';


/** A fixed "now" for the tests: 10 October 2026, 10:00 local time. */
const NOW = new Date(2026, 9, 10, 10, 0, 0, 0);

/**
 * Builds valid form values, with optional overrides.
 * @param overrides Values to replace.
 * @returns The values.
 */
function validValues(overrides: Partial<CreateBookingFormValues> = {}): CreateBookingFormValues {
  return {
    clientName: '  María Pérez ',
    clientPhone: '8888-1111',
    clientEmail: '',
    packageName: ' Retrato familiar ',
    packageTier: 'Standard',
    price: '80 000',
    extras: [],
    discount: '',
    start: new Date(2026, 9, 17, 15, 30, 0, 0),
    durationHours: 2,
    ...overrides,
  };
}

describe('initialFormValues', () => {
  it('proposes tomorrow at the default hour with an empty form', () => {
    const values = initialFormValues(NOW);

    expect(values.start).toEqual(new Date(2026, 9, 11, DEFAULT_START_HOUR, 0, 0, 0));
    expect(values.clientName).toBe('');
    expect(values.durationHours).toBe(2);
    expect(values.packageTier).toBeNull();
    expect(values.extras).toEqual([]);
    expect(values.discount).toBe('');
  });
});

describe('validateCreateBookingForm', () => {
  it('builds the request with trimmed text, normalized phone, whole price and the end from the duration', () => {
    const result = validateCreateBookingForm(validValues(), NOW);

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.request).toEqual({
        clientName: 'María Pérez',
        clientPhone: '+50688881111',
        packageName: 'Retrato familiar',
        packagePrice: 80000,
        currency: 'CRC',
        sessionStart: new Date(2026, 9, 17, 15, 30).toISOString(),
        sessionEnd: new Date(2026, 9, 17, 17, 30).toISOString(),
        packageTier: 'Standard',
      });
    }
  });

  it('sends the normalized email when one is typed and leaves it out when it is blank', () => {
    const withEmail = validateCreateBookingForm(validValues({ clientEmail: '  Maria@Example.COM ' }), NOW);
    const withoutEmail = validateCreateBookingForm(validValues({ clientEmail: '   ' }), NOW);

    expect(withEmail.ok && withEmail.request.clientEmail).toBe('maria@example.com');
    expect(withoutEmail.ok && 'clientEmail' in withoutEmail.request).toBe(false);
  });

  it('rejects an email that is typed but has no valid shape', () => {
    const result = validateCreateBookingForm(validValues({ clientEmail: 'maria@' }), NOW);

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(Object.keys(result.errors)).toEqual(['clientEmail']);
    }
  });

  it('reports every invalid field at once', () => {
    const result = validateCreateBookingForm(
      validValues({ clientName: ' ', clientPhone: '12', packageName: '', packageTier: null, price: '0', start: new Date(2026, 9, 9) }),
      NOW,
    );

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(Object.keys(result.errors).sort()).toEqual(['clientName', 'clientPhone', 'packageName', 'packageTier', 'price', 'start']);
    }
  });

  it('sends the extras with the price they were chosen at, and the discount only when there is one', () => {
    const extras = [{ code: 'makeup', name: 'Maquillaje', unitPrice: 20000, quantity: 2 }];

    const result = validateCreateBookingForm(validValues({ extras, discount: '5 000' }), NOW);

    expect(result.ok && result.request.extras).toEqual(extras);
    expect(result.ok && result.request.discount).toBe(5000);
    const without = validateCreateBookingForm(validValues({ discount: '0' }), NOW);
    expect(without.ok && 'extras' in without.request).toBe(false);
    expect(without.ok && 'discount' in without.request).toBe(false);
  });

  it('asks to choose a package, and says so apart from the type of session', () => {
    const result = validateCreateBookingForm(validValues({ packageTier: null }), NOW);

    expect(!result.ok && result.errors.packageTier).toBe('Elige un paquete.');
    expect(!result.ok && result.errors.packageName).toBeUndefined();
  });

  it('rejects a discount that is not a number or is more than the package and the extras', () => {
    const notANumber = validateCreateBookingForm(validValues({ discount: 'abc' }), NOW);
    const tooBig = validateCreateBookingForm(validValues({ discount: '80 001' }), NOW);
    const equal = validateCreateBookingForm(validValues({ discount: '80 000' }), NOW);

    expect(!notANumber.ok && notANumber.errors.discount).toBe('Escribe un descuento válido o déjalo vacío.');
    expect(!tooBig.ok && tooBig.errors.discount).toMatch(/no puede ser mayor/);
    expect(equal.ok).toBe(true);
  });

  it('counts the extras when checking the discount', () => {
    const extras = [{ code: 'makeup', name: 'Maquillaje', unitPrice: 20000, quantity: 1 }];

    const result = validateCreateBookingForm(validValues({ extras, discount: '100 000' }), NOW);

    expect(result.ok).toBe(true);
  });

  it('rejects a session that starts right now or earlier', () => {
    const result = validateCreateBookingForm(validValues({ start: NOW }), NOW);

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.errors.start).toBe('La sesión debe empezar en el futuro.');
    }
  });
});
