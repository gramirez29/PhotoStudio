import type { PortalBooking } from '../types/api/portal';

/** A valid portal booking as the backend sends it (JSON, before parsing). */
export const portalBookingPayload = {
  photographerName: 'PhotoSense Studio',
  clientName: 'María Pérez',
  packageName: 'Retrato Familiar',
  status: 'Tentative',
  sessionStart: '2026-10-17T21:30:00+00:00',
  sessionEnd: '2026-10-17T23:30:00+00:00',
  expiresAt: '2026-10-19T18:00:00+00:00',
  packagePrice: { amount: 100000, currency: 'CRC' },
  depositRequired: { amount: 50000, currency: 'CRC' },
  totalPaid: { amount: 0, currency: 'CRC' },
  balance: { amount: 100000, currency: 'CRC' },
  contractSigned: false,
  payments: [],
  canSubmitProof: true,
  canReceiveCode: true,
  canSignContract: true,
  basePrice: { amount: 100000, currency: 'CRC' },
  extras: [],
  discount: { amount: 0, currency: 'CRC' },
} as const;

/**
 * Builds a portal booking, already parsed, with optional overrides.
 * @param overrides Values to replace.
 * @returns The booking.
 */
export function portalBooking(overrides: Partial<PortalBooking> = {}): PortalBooking {
  return {
    ...portalBookingPayload,
    payments: [],
    extras: [],
    ...overrides,
  };
}

/** A token as the API issues it: 43 characters of base64url. */
export const SAMPLE_TOKEN = 'k3JhZ8mQ2vXr9TnLwYc5pAeBdUfHs7GxVo1iNtKzMqE';

/**
 * Builds a JSON response.
 * @param status HTTP status.
 * @param body Body to serialize.
 * @param headers Extra headers.
 * @returns The response.
 */
export function jsonResponse(status: number, body: unknown, headers: Record<string, string> = {}): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json', ...headers } });
}
