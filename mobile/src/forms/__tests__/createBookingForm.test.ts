import {
  DEFAULT_START_HOUR,
  initialFormValues,
  normalizePhone,
  parsePrice,
  validateCreateBookingForm,
  withDayOf,
  withTimeOf,
  type CreateBookingFormValues,
} from '../createBookingForm';

const PHOTOGRAPHER_ID = '0197a000-0000-7000-8000-000000000001';

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
    packageName: ' Retrato familiar ',
    price: '80 000',
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
  });
});

describe('normalizePhone', () => {
  it('adds the country code to eight local digits, ignoring separators', () => {
    expect(normalizePhone('8888 1111')).toBe('+50688881111');
    expect(normalizePhone('(8888)-1111')).toBe('+50688881111');
  });

  it('keeps an international number', () => {
    expect(normalizePhone('+1 305 555 0100')).toBe('+13055550100');
  });

  it('accepts the country code typed without the plus sign', () => {
    expect(normalizePhone('506 8888 1111')).toBe('+50688881111');
  });

  it('rejects numbers that are too short or have no recognizable format', () => {
    expect(normalizePhone('')).toBeNull();
    expect(normalizePhone('12345')).toBeNull();
    expect(normalizePhone('+123')).toBeNull();
    expect(normalizePhone('123456789')).toBeNull();
  });
});

describe('parsePrice', () => {
  it('accepts plain, grouped and colon-prefixed amounts', () => {
    expect(parsePrice('80000')).toBe(80000);
    expect(parsePrice('80 000')).toBe(80000);
    expect(parsePrice('₡80.000')).toBe(80000);
    expect(parsePrice('1,250,000')).toBe(1250000);
  });

  it('rejects empty, zero, negative or non numeric values', () => {
    expect(parsePrice('')).toBeNull();
    expect(parsePrice('0')).toBeNull();
    expect(parsePrice('-5')).toBeNull();
    expect(parsePrice('abc')).toBeNull();
    expect(parsePrice('12a')).toBeNull();
  });
});

describe('withDayOf and withTimeOf', () => {
  it('changes the day and keeps the time of day', () => {
    const result = withDayOf(new Date(2026, 9, 17, 15, 30), new Date(2026, 10, 3, 8, 0));

    expect(result).toEqual(new Date(2026, 10, 3, 15, 30, 0, 0));
  });

  it('changes the time of day and keeps the day', () => {
    const result = withTimeOf(new Date(2026, 9, 17, 15, 30), new Date(2020, 0, 1, 9, 15));

    expect(result).toEqual(new Date(2026, 9, 17, 9, 15, 0, 0));
  });
});

describe('validateCreateBookingForm', () => {
  it('builds the request with trimmed text, normalized phone, whole price and the end from the duration', () => {
    const result = validateCreateBookingForm(validValues(), PHOTOGRAPHER_ID, NOW);

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.request).toEqual({
        photographerId: PHOTOGRAPHER_ID,
        clientName: 'María Pérez',
        clientPhone: '+50688881111',
        packageName: 'Retrato familiar',
        packagePrice: 80000,
        currency: 'CRC',
        sessionStart: new Date(2026, 9, 17, 15, 30).toISOString(),
        sessionEnd: new Date(2026, 9, 17, 17, 30).toISOString(),
      });
    }
  });

  it('reports every invalid field at once', () => {
    const result = validateCreateBookingForm(
      validValues({ clientName: ' ', clientPhone: '12', packageName: '', price: '0', start: new Date(2026, 9, 9) }),
      PHOTOGRAPHER_ID,
      NOW,
    );

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(Object.keys(result.errors).sort()).toEqual(['clientName', 'clientPhone', 'packageName', 'price', 'start']);
    }
  });

  it('rejects a session that starts right now or earlier', () => {
    const result = validateCreateBookingForm(validValues({ start: NOW }), PHOTOGRAPHER_ID, NOW);

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.errors.start).toBe('La sesión debe empezar en el futuro.');
    }
  });
});
