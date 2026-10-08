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

  it('lists the bookings of the photographer with the identifier encoded in the query string', async () => {
    const { fetchFn, urls } = stubFetch(jsonResponse(200, [bookingSummaryPayload]));
    const api = createBookingsApi(createHttpClient('https://api.example.test', { fetchFn }));

    const bookings = await api.listBookings('a b');

    expect(urls).toEqual(['https://api.example.test/api/bookings?photographerId=a%20b']);
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
      photographerId: bookingPayload.photographerId,
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
});
