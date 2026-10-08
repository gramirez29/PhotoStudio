import { createBookingsApi, parseBooking } from '../bookingsApi';
import { ResponseShapeError } from '../guards';
import { createHttpClient } from '../httpClient';
import { bookingPayload, jsonResponse, stubFetch } from '../__fixtures__/testResponses';

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

describe('createBookingsApi', () => {
  it('requests the booking by encoded identifier', async () => {
    const { fetchFn, urls } = stubFetch(jsonResponse(200, bookingPayload));
    const api = createBookingsApi(createHttpClient('https://api.example.test', { fetchFn }));

    const booking = await api.getBooking(bookingPayload.id);

    expect(urls).toEqual([`https://api.example.test/api/bookings/${bookingPayload.id}`]);
    expect(booking.id).toBe(bookingPayload.id);
  });
});
