import { describe, expect, it } from 'vitest';
import { ApiError, NetworkError } from '../api/httpClient';
import { ResponseShapeError } from '../api/guards';
import { portalBooking, SAMPLE_TOKEN } from '../test/fixtures';
import { describeLoadError } from './apiErrors';
import { depositDue, hasProofInReview, nextSteps } from './booking';
import { DEFAULT_API_URL, normalizeApiUrl } from './env';
import { formatDateTime, formatDay, formatMoney, formatTimeRange } from './format';
import { parseTokenFromPath } from './route';

describe('parseTokenFromPath', () => {
  it('reads the token of a booking link, with or without a trailing slash', () => {
    expect(parseTokenFromPath(`/b/${SAMPLE_TOKEN}`)).toBe(SAMPLE_TOKEN);
    expect(parseTokenFromPath(`/b/${SAMPLE_TOKEN}/`)).toBe(SAMPLE_TOKEN);
  });

  it.each(['/', '/b', '/b/', '/b/short', `/x/${SAMPLE_TOKEN}`, `/b/${SAMPLE_TOKEN}/extra`, `/b/${SAMPLE_TOKEN}%20`, `/b/${'a'.repeat(129)}`, '/b/has space-aaaaaaaaaaaaaaa'])(
    'finds no token in %p',
    (path) => {
      expect(parseTokenFromPath(path)).toBeNull();
    },
  );
});

describe('normalizeApiUrl', () => {
  it('trims and drops trailing slashes', () => {
    expect(normalizeApiUrl(' https://api.photosensestudio.com// ')).toBe('https://api.photosensestudio.com');
  });

  it('falls back to the local API when the variable is missing or blank', () => {
    expect(normalizeApiUrl(undefined)).toBe(DEFAULT_API_URL);
    expect(normalizeApiUrl('   ')).toBe(DEFAULT_API_URL);
  });
});

describe('format', () => {
  it('formats colones with the symbol', () => {
    expect(formatMoney({ amount: 50000, currency: 'CRC' })).toContain('50');
    expect(formatMoney({ amount: 50000, currency: 'CRC' })).toContain('₡');
  });

  it('shows the session in the Costa Rica time zone, whatever the device uses', () => {
    // 21:30 UTC is 3:30 p. m. in Costa Rica (UTC-6).
    expect(formatTimeRange('2026-10-17T21:30:00+00:00', '2026-10-17T23:30:00+00:00')).toMatch(/3:30.*5:30/);
    expect(formatDateTime('2026-10-17T21:30:00+00:00')).toMatch(/sábado/i);
    expect(formatDateTime('2026-10-17T21:30:00+00:00')).toContain('octubre');
  });

  it('uses the Costa Rica day when UTC is already the next day', () => {
    // 02:00 UTC on the 18th is still the 17th at 8 p. m. in Costa Rica.
    expect(formatDay('2026-10-18T02:00:00+00:00')).toContain('17');
  });
});

describe('depositDue', () => {
  it('is the deposit minus what is verified, never below zero', () => {
    expect(depositDue(portalBooking()).amount).toBe(50000);
    expect(depositDue(portalBooking({ totalPaid: { amount: 20000, currency: 'CRC' } })).amount).toBe(30000);
    expect(depositDue(portalBooking({ totalPaid: { amount: 90000, currency: 'CRC' } })).amount).toBe(0);
  });
});

describe('hasProofInReview', () => {
  it('is true only while a proof waits for the photographer', () => {
    const payment = { amount: { amount: 50000, currency: 'CRC' }, recordedAt: '2026-10-16T12:00:00+00:00' } as const;

    expect(hasProofInReview(portalBooking())).toBe(false);
    expect(hasProofInReview(portalBooking({ payments: [{ ...payment, status: 'PendingVerification' }] }))).toBe(true);
    expect(hasProofInReview(portalBooking({ payments: [{ ...payment, status: 'Verified' }] }))).toBe(false);
  });
});

describe('nextSteps', () => {
  it('tells a tentative booking until when the date is held and how much deposit is missing', () => {
    const steps = nextSteps(portalBooking());

    expect(steps[0]).toContain('apartada hasta');
    expect(steps[1]).toContain('falta el anticipo');
  });

  it('asks for the contract once the deposit is covered', () => {
    const steps = nextSteps(portalBooking({ totalPaid: { amount: 50000, currency: 'CRC' } }));

    expect(steps.some((step) => step.includes('firmar el contrato'))).toBe(true);
    expect(steps.some((step) => step.includes('falta el anticipo'))).toBe(false);
  });

  it('confirms a confirmed booking and shows the balance left', () => {
    const steps = nextSteps(
      portalBooking({ status: 'Confirmed', expiresAt: null, totalPaid: { amount: 50000, currency: 'CRC' }, balance: { amount: 50000, currency: 'CRC' } }),
    );

    expect(steps[0]).toBe('Tu reserva está confirmada.');
    expect(steps[1]).toContain('saldo');
  });

  it('links the balance of a finished session to the final photos', () => {
    const owing = nextSteps(portalBooking({ status: 'Completed', balance: { amount: 30000, currency: 'CRC' } }));
    const paid = nextSteps(portalBooking({ status: 'Completed', balance: { amount: 0, currency: 'CRC' } }));

    expect(owing[0]).toContain('fotos finales');
    expect(paid[0]).toContain('al día');
  });

  it.each(['Cancelled', 'Expired', 'ClientAbsent'] as const)('sends the client to the photographer when the booking is %s', (status) => {
    expect(nextSteps(portalBooking({ status }))[0]).toContain('fotógrafo');
  });

  it('adds that a proof is in review', () => {
    const steps = nextSteps(
      portalBooking({
        payments: [{ amount: { amount: 50000, currency: 'CRC' }, status: 'PendingVerification', recordedAt: '2026-10-16T12:00:00+00:00' }],
      }),
    );

    expect(steps.at(-1)).toContain('en revisión');
  });
});

describe('describeLoadError', () => {
  it('says the link is not valid for a 404, and offers no retry', () => {
    const view = describeLoadError(new ApiError(404, 'x', 'resource.not_found'));

    expect(view.title).toContain('no es válido');
    expect(view.canRetry).toBe(false);
  });

  it.each([
    [new ApiError(429, 'x', 'rate_limit.exceeded'), 'Demasiadas'],
    [new NetworkError(), 'Sin conexión'],
    [new ResponseShapeError('booking.status', 'a string'), 'No pudimos mostrar'],
    [new ApiError(500, 'x', 'server.error'), 'Algo salió mal'],
    [new Error('boom'), 'Algo salió mal'],
  ])('explains %p and offers to retry', (error, title) => {
    const view = describeLoadError(error);

    expect(view.title).toContain(title);
    expect(view.canRetry).toBe(true);
  });
});
