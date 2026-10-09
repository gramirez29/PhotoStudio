import type { BookingResponse } from '../../types/api/booking';
import type { BookingsApi } from '../../types/api/bookingsApi';
import { executeBookingCommand } from '../bookingCommands';
import { parseBooking } from '../bookingsApi';
import { bookingPayload } from '../__fixtures__/testResponses';

/**
 * Builds a bookings API where every method is a mock that answers with the sample booking.
 * @param booking Booking returned by every method.
 * @returns The API.
 */
function stubApi(booking: BookingResponse): BookingsApi {
  return {
    createBooking: jest.fn().mockResolvedValue(booking),
    rescheduleBooking: jest.fn().mockResolvedValue(booking),
    cancelBooking: jest.fn().mockResolvedValue(booking),
    completeBooking: jest.fn().mockResolvedValue(booking),
    markClientAbsent: jest.fn().mockResolvedValue(booking),
    revertClientAbsent: jest.fn().mockResolvedValue(booking),
    signContractInPerson: jest.fn().mockResolvedValue(booking),
    recordInPersonPayment: jest.fn().mockResolvedValue(booking),
    listBookings: jest.fn().mockResolvedValue([]),
    getBooking: jest.fn().mockResolvedValue(booking),
  };
}

describe('executeBookingCommand', () => {
  const booking = parseBooking(bookingPayload);
  const id = booking.id;

  it('cancels with the reason, or with an empty request when there is none', async () => {
    const api = stubApi(booking);

    await executeBookingCommand(api, id, { kind: 'cancel', reason: 'Motivo' });
    await executeBookingCommand(api, id, { kind: 'cancel', reason: null });

    expect(api.cancelBooking).toHaveBeenNthCalledWith(1, id, { reason: 'Motivo' });
    expect(api.cancelBooking).toHaveBeenNthCalledWith(2, id, {});
  });

  it('routes the contract signature and the payment to their endpoints', async () => {
    const api = stubApi(booking);
    const contract = { signerName: 'María Pérez', templateVersion: 'v1', isPaperContract: false };
    const payment = { amount: 50000, currency: 'CRC', method: 'Cash', idempotencyKey: 'pay-1' } as const;

    await executeBookingCommand(api, id, { kind: 'signContract', request: contract });
    await executeBookingCommand(api, id, { kind: 'recordPayment', request: payment });

    expect(api.signContractInPerson).toHaveBeenCalledWith(id, contract);
    expect(api.recordInPersonPayment).toHaveBeenCalledWith(id, payment);
  });

  it('routes complete, mark absent and revert to their endpoints and returns the booking', async () => {
    const api = stubApi(booking);

    const completed = await executeBookingCommand(api, id, { kind: 'complete' });
    await executeBookingCommand(api, id, { kind: 'markClientAbsent' });
    await executeBookingCommand(api, id, { kind: 'revertClientAbsent', reason: 'Por error' });

    expect(completed).toBe(booking);
    expect(api.completeBooking).toHaveBeenCalledWith(id);
    expect(api.markClientAbsent).toHaveBeenCalledWith(id);
    expect(api.revertClientAbsent).toHaveBeenCalledWith(id, { reason: 'Por error' });
    expect(api.cancelBooking).not.toHaveBeenCalled();
  });
});
