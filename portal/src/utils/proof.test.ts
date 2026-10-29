import { describe, expect, it } from 'vitest';
import { ApiError, NetworkError } from '../api/httpClient';
import { portalBooking } from '../test/fixtures';
import type { PortalPayment } from '../types/api/portal';
import { describeCodeRequestError, describeContractLoadError, describeSignContractError, describeSubmitError, describeVerifyError } from './apiErrors';
import { canStartProof, reportableAmount } from './booking';
import { currencySymbol, parseAmount } from './price';
import { INITIAL_PROOF_FLOW, isSessionValid, proofFlowReducer, SESSION_MARGIN_MS } from './proofFlow';

const NOW = Date.parse('2026-10-16T12:00:00+00:00');
const FRESH = { accessToken: 'jwt', expiresAt: new Date(NOW + 30 * 60_000).toISOString() };

/**
 * Builds a payment of the given amount and state.
 * @param amount Amount in colones.
 * @param status State of the payment.
 * @returns The payment.
 */
function payment(amount: number, status: PortalPayment['status']): PortalPayment {
  return { amount: { amount, currency: 'CRC' }, status, recordedAt: '2026-10-16T10:00:00+00:00' };
}

describe('parseAmount', () => {
  it.each([
    ['50000', 50000],
    ['50 000', 50000],
    ['50.000', 50000],
    ['₡50,000', 50000],
    ['  1 250 000 ', 1250000],
  ])('reads %p as %p', (raw, expected) => {
    expect(parseAmount(raw)).toBe(expected);
  });

  it.each(['', '  ', '0', '00', 'abc', '12a', '-5', '5-0', '1e3', '99999999999999999999'])('rejects %p', (raw) => {
    expect(parseAmount(raw)).toBeNull();
  });
});

describe('currencySymbol', () => {
  it('gives the symbol of the currency in the locale', () => {
    expect(currencySymbol('CRC', 'es-CR')).toBe('₡');
  });
});

describe('reportableAmount', () => {
  it('is the balance when nothing is in review', () => {
    expect(reportableAmount(portalBooking())).toBe(100000);
  });

  it('leaves out what is already waiting for the photographer, and counts nothing else', () => {
    const booking = portalBooking({
      payments: [payment(30000, 'PendingVerification'), payment(20000, 'Verified'), payment(10000, 'Rejected')],
    });

    expect(reportableAmount(booking)).toBe(70000);
  });

  it('never goes below zero', () => {
    expect(reportableAmount(portalBooking({ payments: [payment(500000, 'PendingVerification')] }))).toBe(0);
  });
});

describe('canStartProof', () => {
  it('needs the API to allow it, an email for the code and something left to report', () => {
    expect(canStartProof(portalBooking())).toBe(true);
    expect(canStartProof(portalBooking({ canSubmitProof: false }))).toBe(false);
    expect(canStartProof(portalBooking({ canReceiveCode: false }))).toBe(false);
    expect(canStartProof(portalBooking({ payments: [payment(100000, 'PendingVerification')] }))).toBe(false);
  });
});

describe('isSessionValid', () => {
  it('accepts a session far from its expiry', () => {
    expect(isSessionValid(FRESH, NOW)).toBe(true);
  });

  it('refuses a missing one, an expired one and one inside the safety margin', () => {
    expect(isSessionValid(null, NOW)).toBe(false);
    expect(isSessionValid({ ...FRESH, expiresAt: new Date(NOW - 1).toISOString() }, NOW)).toBe(false);
    expect(isSessionValid({ ...FRESH, expiresAt: new Date(NOW + SESSION_MARGIN_MS).toISOString() }, NOW)).toBe(false);
  });
});

describe('proofFlowReducer', () => {
  it('starts by asking for a code', () => {
    expect(proofFlowReducer(INITIAL_PROOF_FLOW, { type: 'open', now: NOW, session: null }).step).toBe('request');
  });

  it('goes from the code sent, to the code verified, to the proof sent', () => {
    const entering = proofFlowReducer({ ...INITIAL_PROOF_FLOW, step: 'request' }, { type: 'codeSent', maskedEmail: 'm***@example.com' });
    const verified = proofFlowReducer(entering, { type: 'verified', session: FRESH });
    const sent = proofFlowReducer(verified, { type: 'proofSent' });

    expect(entering).toMatchObject({ step: 'enter', maskedEmail: 'm***@example.com' });
    expect(verified).toMatchObject({ step: 'form', session: FRESH });
    expect(sent.step).toBe('sent');
  });

  it('skips the code when the page already has a valid session', () => {
    const state = proofFlowReducer(INITIAL_PROOF_FLOW, { type: 'open', now: NOW, session: FRESH });

    expect(state).toMatchObject({ step: 'form', session: FRESH });
  });

  it('asks for a new code when the session of the page expired', () => {
    const expired = { ...FRESH, expiresAt: new Date(NOW - 1000).toISOString() };

    expect(proofFlowReducer(INITIAL_PROOF_FLOW, { type: 'open', now: NOW, session: expired })).toMatchObject({ step: 'request', session: null });
  });

  it('drops the session when it expires halfway', () => {
    const inForm = { step: 'form', maskedEmail: null, session: FRESH } as const;

    expect(proofFlowReducer(inForm, { type: 'sessionExpired' })).toMatchObject({ step: 'request', session: null });
  });

  it('closing keeps the session so the next proof does not need another code', () => {
    const inForm = { step: 'form', maskedEmail: null, session: FRESH } as const;

    expect(proofFlowReducer(inForm, { type: 'close' })).toMatchObject({ step: 'closed', session: FRESH });
  });
});

describe('describeCodeRequestError', () => {
  it('says the photographer must add the email', () => {
    expect(describeCodeRequestError(new ApiError(409, 'x', 'portal.email_missing')).message).toContain('correo registrado');
  });

  it('keeps the wait the server asks for', () => {
    const view = describeCodeRequestError(new ApiError(429, 'x', 'portal.otp_rate_limited', 42));

    expect(view.retryAfterSeconds).toBe(42);
    expect(view.message).toContain('hace poco');
  });

  it('explains a failed email, a rate limit, a dead link and a lost connection', () => {
    expect(describeCodeRequestError(new ApiError(503, 'x', 'email.delivery_failed')).message).toContain('correo');
    expect(describeCodeRequestError(new ApiError(429, 'x', 'rate_limit.exceeded', 30))).toMatchObject({ retryAfterSeconds: 30 });
    expect(describeCodeRequestError(new ApiError(404, 'x', 'resource.not_found')).message).toContain('ya no es válido');
    expect(describeCodeRequestError(new NetworkError()).message).toContain('internet');
    expect(describeCodeRequestError(new Error('boom')).message).toContain('No pudimos enviar');
  });
});

describe('describeVerifyError', () => {
  it('does not say whether the code was wrong or expired', () => {
    const view = describeVerifyError(new ApiError(422, 'x', 'portal.invalid_code'));

    expect(view.message).toContain('no es correcto o ya venció');
  });

  it('falls back to a generic message', () => {
    expect(describeVerifyError(new Error('boom')).message).toContain('No pudimos verificar');
  });
});

describe('describeSubmitError', () => {
  it('marks an expired verification, so the client confirms a code again', () => {
    const view = describeSubmitError(new ApiError(401, 'x', null));

    expect(view.sessionExpired).toBe(true);
    expect(view.message).toContain('se conserva');
  });

  it('explains each business refusal without expiring the session', () => {
    expect(describeSubmitError(new ApiError(422, 'x', 'money.invalid_amount'))).toMatchObject({ sessionExpired: false });
    expect(describeSubmitError(new ApiError(409, 'x', 'booking.invalid_transition')).message).toContain('ya no recibe pagos');
    expect(describeSubmitError(new ApiError(409, 'x', 'concurrency.conflict')).message).toContain('cambió');
    expect(describeSubmitError(new NetworkError()).message).toContain('internet');
    expect(describeSubmitError(new Error('boom')).message).toContain('No pudimos registrar');
  });
});

describe('describeSignContractError', () => {
  it('asks to confirm the email again when the session is refused, keeping what was typed', () => {
    const view = describeSignContractError(new ApiError(401, 'x', null));

    expect(view.sessionExpired).toBe(true);
    expect(view.message).toMatch(/lo que escribiste se conserva/);
  });

  it.each([
    ['contract.template_changed', /cambió mientras lo leías/],
    ['booking.contract_already_signed', /ya está firmado/],
    ['contract.invalid_id_number', /cédula o el DIMEX/],
    ['contract.invalid_details', /número de personas/],
    ['booking.invalid_transition', /ya no se puede firmar/],
    ['concurrency.conflict', /cambió mientras firmabas/],
  ])('explains %s', (code, message) => {
    const view = describeSignContractError(new ApiError(409, 'x', code));

    expect(view.sessionExpired).toBe(false);
    expect(view.message).toMatch(message);
  });

  it('explains a lost connection and an error nobody planned for', () => {
    expect(describeSignContractError(new NetworkError()).message).toMatch(/No pudimos conectarnos/);
    expect(describeSignContractError(new ApiError(500, 'x', null)).message).toMatch(/No pudimos registrar tu firma/);
  });
});

describe('describeContractLoadError', () => {
  it('asks to confirm the email again when the session is refused', () => {
    expect(describeContractLoadError(new ApiError(401, 'x', null)).sessionExpired).toBe(true);
  });

  it('explains any other failure as a contract that could not be loaded', () => {
    const view = describeContractLoadError(new ApiError(500, 'x', null));

    expect(view.sessionExpired).toBe(false);
    expect(view.message).toMatch(/No pudimos cargar el contrato/);
  });
});
