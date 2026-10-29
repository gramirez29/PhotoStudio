import { createBookingsApi, parseBooking, parseBookingSummaries } from '../bookingsApi';
import { ResponseShapeError } from '../guards';
import { createHttpClient } from '../httpClient';
import { bookingPayload, bookingSummaryPayload, jsonResponse, stubFetch } from '../__fixtures__/testResponses';

describe('parseBooking', () => {
  it('parses a valid backend payload', () => {
    const booking = parseBooking(bookingPayload);

    expect(booking.status).toBe('Confirmed');
    expect(booking.balance).toEqual({ amount: 50000, currency: 'CRC' });
    expect(booking.contract?.channel).toBe('InPerson');
    expect(booking.payments).toHaveLength(1);
    expect(booking.allowedActions).toEqual(['RecordInPersonPayment', 'Reschedule', 'Cancel']);
  });

  it('reads the client email and the reference of a proof, and treats them as null when the backend sends none', () => {
    const withBoth = parseBooking({
      ...bookingPayload,
      clientEmail: 'maria@example.com',
      payments: [{ ...bookingPayload.payments[0], status: 'PendingVerification', reference: 'SINPE-123' }],
    });
    const withNeither = parseBooking(bookingPayload);

    expect(withBoth.clientEmail).toBe('maria@example.com');
    expect(withBoth.payments[0]?.reference).toBe('SINPE-123');
    expect(withNeither.clientEmail).toBeNull();
    expect(withNeither.payments[0]?.reference).toBeNull();
  });

  it('reads what the client declared when they signed through the link', () => {
    const contract = {
      ...bookingPayload.contract,
      channel: 'Portal',
      declaration: { idNumber: '112345678', participantCount: 3, sessionLocation: 'Parque La Sabana', imageUse: 'Authorized' },
    };

    const booking = parseBooking({ ...bookingPayload, contract });

    expect(booking.contract?.declaration).toEqual({
      idNumber: '112345678',
      participantCount: 3,
      sessionLocation: 'Parque La Sabana',
      imageUse: 'Authorized',
    });
  });

  it('treats a missing or null declaration, and a missing location, as nothing declared', () => {
    const { declaration: omitted, ...withoutField } = bookingPayload.contract;
    const declaration = { idNumber: '112345678', participantCount: 1, sessionLocation: null, imageUse: 'NotAuthorized' };

    expect(omitted).toBeNull();
    expect(parseBooking({ ...bookingPayload, contract: withoutField }).contract?.declaration).toBeNull();
    expect(parseBooking({ ...bookingPayload, contract: { ...withoutField, declaration: null } }).contract?.declaration).toBeNull();
    expect(parseBooking({ ...bookingPayload, contract: { ...withoutField, declaration } }).contract?.declaration?.sessionLocation).toBeNull();
  });

  it('rejects a declaration with an image use the app does not know', () => {
    const declaration = { idNumber: '112345678', participantCount: 1, sessionLocation: null, imageUse: 'Maybe' };

    expect(() => parseBooking({ ...bookingPayload, contract: { ...bookingPayload.contract, declaration } })).toThrow(ResponseShapeError);
  });

  it('accepts a booking without contract', () => {
    const booking = parseBooking({ ...bookingPayload, contract: null });

    expect(booking.contract).toBeNull();
  });

  it('rejects an unknown status', () => {
    expect(() => parseBooking({ ...bookingPayload, status: 'Archived' })).toThrow(ResponseShapeError);
  });

  it('rejects an unknown allowed action', () => {
    expect(() => parseBooking({ ...bookingPayload, allowedActions: ['Fly'] })).toThrow(
      'booking.allowedActions[0].action',
    );
  });

  it('rejects a missing amount', () => {
    expect(() => parseBooking({ ...bookingPayload, balance: { currency: 'CRC' } })).toThrow(
      'booking.balance.amount',
    );
  });
});

describe('parseBookingSummaries', () => {
  it('parses a list and keeps the order sent by the backend', () => {
    const second = { ...bookingSummaryPayload, id: 'second', clientName: 'Luis Mora', status: 'Tentative' };

    const summaries = parseBookingSummaries([bookingSummaryPayload, second]);

    expect(summaries.map((summary) => summary.clientName)).toEqual(['María Pérez', 'Luis Mora']);
    expect(summaries[1]?.status).toBe('Tentative');
    expect(summaries[0]?.balance).toEqual({ amount: 50000, currency: 'CRC' });
  });

  it('accepts an empty list', () => {
    expect(parseBookingSummaries([])).toEqual([]);
  });

  it('rejects a body that is not an array', () => {
    expect(() => parseBookingSummaries({ items: [] })).toThrow('bookings should be an array');
  });

  it('points to the element that is invalid', () => {
    expect(() => parseBookingSummaries([bookingSummaryPayload, { ...bookingSummaryPayload, status: 'Archived' }])).toThrow(
      'bookings[1].status',
    );
  });
});

describe('createBookingsApi', () => {
  it('requests the booking by encoded identifier', async () => {
    const { fetchFn, urls } = stubFetch(jsonResponse(200, bookingPayload));
    const api = createBookingsApi(createHttpClient('https://api.example.test', { fetchFn }));

    const booking = await api.getBooking(bookingPayload.id);

    expect(urls).toEqual([`https://api.example.test/api/bookings/${bookingPayload.id}`]);
    expect(booking.id).toBe(bookingPayload.id);
  });

  it('lists the bookings of the signed-in photographer without sending an identifier', async () => {
    const { fetchFn, urls } = stubFetch(jsonResponse(200, [bookingSummaryPayload]));
    const api = createBookingsApi(createHttpClient('https://api.example.test', { fetchFn }));

    const bookings = await api.listBookings();

    expect(urls).toEqual(['https://api.example.test/api/bookings']);
    expect(bookings).toHaveLength(1);
  });

  it('posts the booking request as JSON and parses the created booking', async () => {
    const requests: { readonly url: string; readonly init: RequestInit | undefined }[] = [];
    const client = createHttpClient('https://api.example.test', {
      fetchFn: (url, init) => {
        requests.push({ url, init });
        return Promise.resolve(jsonResponse(201, bookingPayload));
      },
    });
    const request = {
      clientName: 'María Pérez',
      clientPhone: '+50688881111',
      packageName: 'Retrato familiar',
      packagePrice: 100000,
      currency: 'CRC',
      sessionStart: '2026-10-11T12:00:00.000Z',
      sessionEnd: '2026-10-11T14:00:00.000Z',
    };

    const booking = await createBookingsApi(client).createBooking(request);

    expect(requests).toHaveLength(1);
    expect(requests[0]?.url).toBe('https://api.example.test/api/bookings');
    expect(requests[0]?.init?.method).toBe('POST');
    expect(JSON.parse(String(requests[0]?.init?.body))).toEqual(request);
    expect(booking.id).toBe(bookingPayload.id);
  });

  it('posts the new slot to the reschedule endpoint of the booking', async () => {
    const requests: { readonly url: string; readonly init: RequestInit | undefined }[] = [];
    const client = createHttpClient('https://api.example.test', {
      fetchFn: (url, init) => {
        requests.push({ url, init });
        return Promise.resolve(jsonResponse(200, bookingPayload));
      },
    });
    const request = { sessionStart: '2026-10-20T15:00:00.000Z', sessionEnd: '2026-10-20T19:00:00.000Z' };

    const booking = await createBookingsApi(client).rescheduleBooking(bookingPayload.id, request);

    expect(requests[0]?.url).toBe(`https://api.example.test/api/bookings/${bookingPayload.id}/reschedule`);
    expect(requests[0]?.init?.method).toBe('POST');
    expect(JSON.parse(String(requests[0]?.init?.body))).toEqual(request);
    expect(booking.id).toBe(bookingPayload.id);
  });

  describe('actions on a booking', () => {
    /**
     * Builds an API whose HTTP client records every request and answers with the sample booking.
     * @returns The API and the recorded requests.
     */
    function recordingApi(): {
      readonly api: ReturnType<typeof createBookingsApi>;
      readonly requests: { readonly url: string; readonly init: RequestInit | undefined }[];
    } {
      const requests: { readonly url: string; readonly init: RequestInit | undefined }[] = [];
      const client = createHttpClient('https://api.example.test', {
        fetchFn: (url, init) => {
          requests.push({ url, init });
          return Promise.resolve(jsonResponse(200, bookingPayload));
        },
      });
      return { api: createBookingsApi(client), requests };
    }

    const base = `https://api.example.test/api/bookings/${bookingPayload.id}`;

    it('cancels with a reason, or with an empty JSON object when there is none', async () => {
      const { api, requests } = recordingApi();

      await api.cancelBooking(bookingPayload.id, { reason: 'El cliente cambió de planes' });
      await api.cancelBooking(bookingPayload.id, {});

      expect(requests.map((request) => request.url)).toEqual([`${base}/cancel`, `${base}/cancel`]);
      expect(JSON.parse(String(requests[0]?.init?.body))).toEqual({ reason: 'El cliente cambió de planes' });
      expect(requests[1]?.init?.body).toBe('{}');
    });

    it('completes and marks the client absent with a POST that has no body', async () => {
      const { api, requests } = recordingApi();

      await api.completeBooking(bookingPayload.id);
      await api.markClientAbsent(bookingPayload.id);

      expect(requests.map((request) => request.url)).toEqual([`${base}/complete`, `${base}/client-absent`]);
      expect(requests.map((request) => request.init?.method)).toEqual(['POST', 'POST']);
      expect(requests.map((request) => request.init?.body)).toEqual([undefined, undefined]);
    });

    it('reverts the absence with the reason as JSON', async () => {
      const { api, requests } = recordingApi();

      const booking = await api.revertClientAbsent(bookingPayload.id, { reason: 'Marcado por error' });

      expect(requests[0]?.url).toBe(`${base}/client-absent/revert`);
      expect(JSON.parse(String(requests[0]?.init?.body))).toEqual({ reason: 'Marcado por error' });
      expect(booking.id).toBe(bookingPayload.id);
    });

    it('posts the contract signature to the in-person contract endpoint', async () => {
      const { api, requests } = recordingApi();
      const request = { signerName: 'María Pérez', templateVersion: 'v1', isPaperContract: false };

      await api.signContractInPerson(bookingPayload.id, request);

      expect(requests[0]?.url).toBe(`${base}/contract/in-person`);
      expect(requests[0]?.init?.method).toBe('POST');
      expect(JSON.parse(String(requests[0]?.init?.body))).toEqual(request);
    });

    it('posts to the link endpoint with no body and parses the link', async () => {
      const requests: { readonly url: string; readonly init: RequestInit | undefined }[] = [];
      const link = { token: 'secret', url: 'https://book.example.test/b/secret', expiresAt: '2027-01-01T00:00:00+00:00' };
      const client = createHttpClient('https://api.example.test', {
        fetchFn: (url, init) => {
          requests.push({ url, init });
          return Promise.resolve(jsonResponse(201, link));
        },
      });

      const created = await createBookingsApi(client).createBookingLink(bookingPayload.id);

      expect(requests[0]?.url).toBe(`${base}/link`);
      expect(requests[0]?.init?.method).toBe('POST');
      expect(requests[0]?.init?.body).toBeUndefined();
      expect(created).toEqual(link);
    });

    it('verifies a payment with a POST that has no body, and rejects it with the reason as JSON', async () => {
      const { api, requests } = recordingApi();
      const paymentId = '0199a1b2-0000-7000-8000-000000000003';

      await api.verifyPayment(bookingPayload.id, paymentId);
      await api.rejectPayment(bookingPayload.id, paymentId, { reason: 'No aparece en el banco' });

      expect(requests.map((request) => request.url)).toEqual([
        `${base}/payments/${paymentId}/verify`,
        `${base}/payments/${paymentId}/reject`,
      ]);
      expect(requests[0]?.init?.body).toBeUndefined();
      expect(JSON.parse(String(requests[1]?.init?.body))).toEqual({ reason: 'No aparece en el banco' });
    });

    it('posts the payment with its idempotency key to the in-person payment endpoint', async () => {
      const { api, requests } = recordingApi();
      const request = { amount: 50000, currency: 'CRC', method: 'Cash', idempotencyKey: 'pay-1' } as const;

      await api.recordInPersonPayment(bookingPayload.id, request);

      expect(requests[0]?.url).toBe(`${base}/payments/in-person`);
      expect(JSON.parse(String(requests[0]?.init?.body))).toEqual(request);
    });
  });
});
