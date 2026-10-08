import type { FetchFunction } from '../../types/api/http';

/**
 * Builds a JSON response like the ones the backend returns.
 * @param status HTTP status code.
 * @param body Value serialized as the response body.
 * @param contentType Content type header.
 * @returns The response.
 */
export function jsonResponse(status: number, body: unknown, contentType = 'application/json'): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': contentType } });
}

/**
 * Builds a plain text response.
 * @param status HTTP status code.
 * @param text Response body.
 * @returns The response.
 */
export function textResponse(status: number, text: string): Response {
  return new Response(text, { status, headers: { 'Content-Type': 'text/plain' } });
}

/**
 * Creates a fetch stub that always returns the given response and records the requested URLs.
 * @param response Response to return.
 * @returns The stub and the list of requested URLs.
 */
export function stubFetch(response: Response): { readonly fetchFn: FetchFunction; readonly urls: string[] } {
  const urls: string[] = [];
  const fetchFn: FetchFunction = (input) => {
    urls.push(input);
    return Promise.resolve(response);
  };

  return { fetchFn, urls };
}

/** A valid booking payload as produced by the backend. */
export const bookingPayload = {
  id: '0199a1b2-0000-7000-8000-000000000001',
  photographerId: '0199a1b2-0000-7000-8000-000000000002',
  clientName: 'María Pérez',
  clientPhone: '+506 8888-8888',
  packageName: 'Retrato familiar',
  status: 'Confirmed',
  sessionStart: '2026-10-11T12:00:00+00:00',
  sessionEnd: '2026-10-11T14:00:00+00:00',
  expiresAt: null,
  packagePrice: { amount: 100000, currency: 'CRC' },
  depositRequired: { amount: 50000, currency: 'CRC' },
  totalPaid: { amount: 50000, currency: 'CRC' },
  balance: { amount: 50000, currency: 'CRC' },
  contract: {
    signerName: 'María Pérez',
    templateVersion: 'v1',
    channel: 'InPerson',
    signedAt: '2026-10-01T12:00:00+00:00',
  },
  payments: [
    {
      id: '0199a1b2-0000-7000-8000-000000000003',
      amount: { amount: 50000, currency: 'CRC' },
      method: 'Cash',
      channel: 'InPerson',
      status: 'Verified',
      recordedAt: '2026-10-01T12:00:00+00:00',
    },
  ],
  allowedActions: ['RecordInPersonPayment', 'Reschedule', 'Cancel'],
} as const;

/** A valid booking summary as produced by the list endpoint of the backend. */
export const bookingSummaryPayload = {
  id: '0199a1b2-0000-7000-8000-000000000001',
  clientName: 'María Pérez',
  packageName: 'Retrato familiar',
  status: 'Confirmed',
  sessionStart: '2026-10-11T12:00:00+00:00',
  sessionEnd: '2026-10-11T14:00:00+00:00',
  packagePrice: { amount: 100000, currency: 'CRC' },
  balance: { amount: 50000, currency: 'CRC' },
} as const;
