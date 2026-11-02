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

/** A gallery as the web area of the photographer receives it (JSON, before parsing). */
export const galleryPayload = {
  id: '0197a000-0000-7000-8000-000000000001',
  bookingId: '0197a000-0000-7000-8000-0000000000b1',
  status: 'Pending',
  clientName: 'María Pérez',
  packageName: 'Retrato Familiar',
  sessionStart: '2026-10-17T21:30:00+00:00',
  includedPhotos: 30,
  bonusPhotos: 5,
  selectablePhotos: 35,
  extraPhotoPrice: { amount: 3000, currency: 'CRC' },
  maxPhotos: 150,
  photoCount: 0,
  selectedPhotoCount: 0,
  extraPhotoCount: 0,
  selectionSubmittedAt: null,
  createdAt: '2026-10-17T23:45:00+00:00',
  allowedActions: ['EditTerms'],
} as const;

/** The photographer of a web session as the API sends them. */
export const studioUserPayload = { photographerId: '0197a000-0000-7000-8000-0000000000aa', username: 'ana', name: 'Ana Pérez', maskedEmail: '' } as const;

/**
 * Builds a gallery payload with another identifier and client, so a list has more than one.
 * @param index Number that tells the galleries apart.
 * @returns The payload.
 */
export function galleryPayloadNumber(index: number): Omit<typeof galleryPayload, 'id' | 'clientName'> & { readonly id: string; readonly clientName: string } {
  return { ...galleryPayload, id: `0197a000-0000-7000-8000-00000000010${index}`, clientName: `Cliente ${index}` };
}
