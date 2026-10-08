import { ApiError } from '../../api/httpClient';
import { NETWORK_ERROR_MESSAGE, createBookingErrorMessage } from '../apiErrors';

describe('createBookingErrorMessage', () => {
  it('explains a taken slot', () => {
    const error = new ApiError(409, 'The photographer already has a booking in that slot.', 'booking.slot_unavailable');

    expect(createBookingErrorMessage(error)).toBe('Ya tienes una reserva en ese horario. Elige otra hora.');
  });

  it('explains a session in the past', () => {
    expect(createBookingErrorMessage(new ApiError(422, 'x', 'booking.session_in_past'))).toBe(
      'La sesión debe empezar en el futuro.',
    );
  });

  it('falls back to a generic message for unknown codes and for errors without code', () => {
    const generic = 'No se pudo crear la reserva. Inténtalo de nuevo.';

    expect(createBookingErrorMessage(new ApiError(500, 'x', 'server.error'))).toBe(generic);
    expect(createBookingErrorMessage(new ApiError(502, 'x', null))).toBe(generic);
  });

  it('reports connection problems when the error did not come from the API', () => {
    expect(createBookingErrorMessage(new TypeError('Network request failed'))).toBe(NETWORK_ERROR_MESSAGE);
  });
});
