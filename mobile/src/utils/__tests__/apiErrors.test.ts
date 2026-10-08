import { ApiError } from '../../api/httpClient';
import { NETWORK_ERROR_MESSAGE, createBookingErrorMessage, rescheduleBookingErrorMessage } from '../apiErrors';

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

describe('rescheduleBookingErrorMessage', () => {
  it('explains a taken slot, a booking that is not confirmed and a concurrent change', () => {
    expect(rescheduleBookingErrorMessage(new ApiError(409, 'x', 'booking.slot_unavailable'))).toBe(
      'Ya tienes otra reserva en ese horario. Elige otra hora.',
    );
    expect(rescheduleBookingErrorMessage(new ApiError(409, 'x', 'booking.invalid_transition'))).toBe(
      'Solo se pueden reprogramar las reservas confirmadas.',
    );
    expect(rescheduleBookingErrorMessage(new ApiError(409, 'x', 'concurrency.conflict'))).toContain('cambió');
  });

  it('explains a missing booking and a session in the past', () => {
    expect(rescheduleBookingErrorMessage(new ApiError(404, 'x', 'resource.not_found'))).toBe('La reserva ya no existe.');
    expect(rescheduleBookingErrorMessage(new ApiError(422, 'x', 'booking.session_in_past'))).toBe(
      'La sesión debe empezar en el futuro.',
    );
  });

  it('falls back to a generic message and reports connection problems', () => {
    expect(rescheduleBookingErrorMessage(new ApiError(500, 'x', null))).toBe(
      'No se pudo reprogramar la reserva. Inténtalo de nuevo.',
    );
    expect(rescheduleBookingErrorMessage(new TypeError('Network request failed'))).toBe(NETWORK_ERROR_MESSAGE);
  });
});
