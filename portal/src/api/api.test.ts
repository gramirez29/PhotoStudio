import { describe, expect, it, vi } from 'vitest';
import { jsonResponse, portalBookingPayload, SAMPLE_TOKEN } from '../test/fixtures';
import { ResponseShapeError } from './guards';
import { ApiError, createHttpClient, NetworkError } from './httpClient';
import { createPortalApi, parsePortalBooking } from './portalApi';
import { MAX_RETRIES, shouldRetry } from './queryClient';

describe('parsePortalBooking', () => {
  it('parses a valid backend payload', () => {
    const booking = parsePortalBooking(portalBookingPayload);

    expect(booking.status).toBe('Tentative');
    expect(booking.balance).toEqual({ amount: 100000, currency: 'CRC' });
    expect(booking.expiresAt).toBe('2026-10-19T18:00:00+00:00');
    expect(booking.canReceiveCode).toBe(true);
  });

  it('accepts no expiry and parses the payments', () => {
    const booking = parsePortalBooking({
      ...portalBookingPayload,
      expiresAt: null,
      payments: [{ amount: { amount: 1, currency: 'CRC' }, status: 'PendingVerification', recordedAt: '2026-10-16T12:00:00+00:00' }],
    });

    expect(booking.expiresAt).toBeNull();
    expect(booking.payments[0]?.status).toBe('PendingVerification');
  });

  it.each([
    ['an unknown status', { status: 'Mystery' }, 'booking.status'],
    ['a missing amount', { balance: undefined }, 'booking.balance'],
    ['a text amount', { balance: { amount: '5', currency: 'CRC' } }, 'booking.balance.amount'],
    ['a payment with an unknown state', { payments: [{ amount: { amount: 1, currency: 'CRC' }, status: 'Lost', recordedAt: 'x' }] }, 'booking.payments[0].status'],
    ['a flag that is not a boolean', { canSubmitProof: 'yes' }, 'booking.canSubmitProof'],
  ])('rejects %s and says where', (_name, overrides, path) => {
    expect(() => parsePortalBooking({ ...portalBookingPayload, ...overrides })).toThrow(ResponseShapeError);
    expect(() => parsePortalBooking({ ...portalBookingPayload, ...overrides })).toThrow(path);
  });

  it('rejects a body that is not an object', () => {
    expect(() => parsePortalBooking(null)).toThrow(ResponseShapeError);
    expect(() => parsePortalBooking([])).toThrow(ResponseShapeError);
  });
});

describe('createHttpClient', () => {
  it('requests the address under the base, asks for JSON and parses the answer', async () => {
    const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(200, { value: 7 }));
    const client = createHttpClient('https://api.example.test', { fetchFn });

    const result = await client.get('/thing', (body) => body);

    expect(result).toEqual({ value: 7 });
    expect(fetchFn).toHaveBeenCalledWith('https://api.example.test/thing', expect.objectContaining({ method: 'GET' }));
  });

  it('turns an error answer into an ApiError with the code and the Retry-After', async () => {
    const fetchFn = vi
      .fn<typeof fetch>()
      .mockResolvedValue(jsonResponse(429, { detail: 'Wait', code: 'portal.otp_rate_limited' }, { 'Retry-After': '42' }));
    const client = createHttpClient('https://api.example.test', { fetchFn });

    const error = await client.get('/x', (body) => body).catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 429, code: 'portal.otp_rate_limited', retryAfterSeconds: 42, message: 'Wait' });
  });

  it('keeps going when the error answer is not JSON', async () => {
    const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(new Response('<html>bad gateway</html>', { status: 502 }));
    const client = createHttpClient('https://api.example.test', { fetchFn });

    const error = await client.get('/x', (body) => body).catch((caught: unknown) => caught);

    expect(error).toMatchObject({ status: 502, code: null, retryAfterSeconds: null });
  });

  it('ignores a Retry-After that is not a number of seconds', async () => {
    const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(429, {}, { 'Retry-After': 'Wed, 21 Oct 2026 07:28:00 GMT' }));
    const client = createHttpClient('https://api.example.test', { fetchFn });

    const error = await client.get('/x', (body) => body).catch((caught: unknown) => caught);

    expect(error).toMatchObject({ retryAfterSeconds: null });
  });

  it('reports a request that got no answer as a NetworkError', async () => {
    const fetchFn = vi.fn<typeof fetch>().mockRejectedValue(new TypeError('Failed to fetch'));
    const client = createHttpClient('https://api.example.test', { fetchFn });

    await expect(client.get('/x', (body) => body)).rejects.toBeInstanceOf(NetworkError);
  });

  it('lets a cancelled request through as an abort, not as a network failure', async () => {
    const fetchFn = vi.fn<typeof fetch>().mockRejectedValue(new DOMException('Aborted', 'AbortError'));
    const client = createHttpClient('https://api.example.test', { fetchFn });

    await expect(client.get('/x', (body) => body)).rejects.toMatchObject({ name: 'AbortError' });
  });
});

describe('createPortalApi', () => {
  it('reads the booking of a token, encoded, from the public endpoint', async () => {
    const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(200, portalBookingPayload));
    const api = createPortalApi(createHttpClient('https://api.example.test', { fetchFn }));

    const booking = await api.getBooking(SAMPLE_TOKEN);

    expect(fetchFn.mock.calls[0]?.[0]).toBe(`https://api.example.test/public/bookings/${SAMPLE_TOKEN}`);
    expect(booking.packageName).toBe('Retrato Familiar');
  });

  it('never lets a token change the path', async () => {
    const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(200, portalBookingPayload));
    const api = createPortalApi(createHttpClient('https://api.example.test', { fetchFn }));

    await api.getBooking('../../api/bookings');

    expect(fetchFn.mock.calls[0]?.[0]).toBe('https://api.example.test/public/bookings/..%2F..%2Fapi%2Fbookings');
  });
});

describe('shouldRetry', () => {
  it('does not repeat an answer that says the request itself is wrong', () => {
    expect(shouldRetry(0, new ApiError(404, 'x', null))).toBe(false);
    expect(shouldRetry(0, new ApiError(429, 'x', null))).toBe(false);
  });

  it('repeats a network or server failure, up to the limit', () => {
    expect(shouldRetry(0, new NetworkError())).toBe(true);
    expect(shouldRetry(1, new ApiError(503, 'x', null))).toBe(true);
    expect(shouldRetry(MAX_RETRIES, new NetworkError())).toBe(false);
  });
});

describe('POST requests', () => {
  it('sends the JSON body and the session token as a bearer', async () => {
    const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(200, { ok: true }));
    const client = createHttpClient('https://api.example.test', { fetchFn });

    await client.post('/thing', { amount: 5 }, (body) => body, { accessToken: 'jwt' });

    const init = fetchFn.mock.calls[0]?.[1];
    expect(init?.method).toBe('POST');
    expect(init?.body).toBe('{"amount":5}');
    expect(init?.headers).toMatchObject({ 'Content-Type': 'application/json', Authorization: 'Bearer jwt' });
  });

  it('sends no body, no content type and no token when there is none', async () => {
    const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(202, { ok: true }));
    const client = createHttpClient('https://api.example.test', { fetchFn });

    await client.post('/thing', undefined, (body) => body);

    const init = fetchFn.mock.calls[0]?.[1];
    expect(init?.body).toBeUndefined();
    expect(init?.headers).toEqual({ Accept: 'application/json' });
  });

  it('turns an error answer into an ApiError', async () => {
    const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(422, { detail: 'Wrong', code: 'portal.invalid_code' }));
    const client = createHttpClient('https://api.example.test', { fetchFn });

    await expect(client.post('/thing', { code: '1' }, (body) => body)).rejects.toMatchObject({ status: 422, code: 'portal.invalid_code' });
  });
});

describe('portal API, code and proof', () => {
  const sent = { maskedEmail: 'm***@example.com', expiresAt: '2026-10-16T12:10:00+00:00' };
  const session = { accessToken: 'jwt', expiresAt: '2026-10-16T12:30:00+00:00' };

  /**
   * Builds an API whose HTTP client records the requests and answers with the given body.
   * @param status HTTP status of the answer.
   * @param body Body of the answer.
   * @returns The API and the fetch mock.
   */
  function apiAnswering(status: number, body: unknown): { readonly api: ReturnType<typeof createPortalApi>; readonly fetchFn: ReturnType<typeof vi.fn<typeof fetch>> } {
    const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(status, body));
    return { api: createPortalApi(createHttpClient('https://api.example.test', { fetchFn })), fetchFn };
  }

  it('asks for a code with a POST that has no body', async () => {
    const { api, fetchFn } = apiAnswering(202, sent);

    const result = await api.requestCode(SAMPLE_TOKEN);

    expect(fetchFn.mock.calls[0]?.[0]).toBe(`https://api.example.test/public/bookings/${SAMPLE_TOKEN}/code`);
    expect(fetchFn.mock.calls[0]?.[1]?.body).toBeUndefined();
    expect(result).toEqual(sent);
  });

  it('verifies the code as JSON and parses the session', async () => {
    const { api, fetchFn } = apiAnswering(200, session);

    const result = await api.verifyCode(SAMPLE_TOKEN, '123456');

    expect(fetchFn.mock.calls[0]?.[0]).toBe(`https://api.example.test/public/bookings/${SAMPLE_TOKEN}/code/verify`);
    expect(JSON.parse(String(fetchFn.mock.calls[0]?.[1]?.body))).toEqual({ code: '123456' });
    expect(result).toEqual(session);
  });

  it('submits the proof with the session token and parses the booking', async () => {
    const { api, fetchFn } = apiAnswering(200, portalBookingPayload);

    const booking = await api.submitProof(SAMPLE_TOKEN, 'jwt', { amount: 50000, reference: 'R1', idempotencyKey: 'k' });

    expect(fetchFn.mock.calls[0]?.[0]).toBe(`https://api.example.test/public/bookings/${SAMPLE_TOKEN}/payments`);
    expect(fetchFn.mock.calls[0]?.[1]?.headers).toMatchObject({ Authorization: 'Bearer jwt' });
    expect(JSON.parse(String(fetchFn.mock.calls[0]?.[1]?.body))).toEqual({ amount: 50000, reference: 'R1', idempotencyKey: 'k' });
    expect(booking.packageName).toBe('Retrato Familiar');
  });

  it('rejects an answer that does not match the contract', async () => {
    await expect(apiAnswering(202, { maskedEmail: 5 }).api.requestCode(SAMPLE_TOKEN)).rejects.toBeInstanceOf(ResponseShapeError);
    await expect(apiAnswering(200, { accessToken: 'x' }).api.verifyCode(SAMPLE_TOKEN, '123456')).rejects.toBeInstanceOf(ResponseShapeError);
  });
});
