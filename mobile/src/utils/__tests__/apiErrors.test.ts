import { ApiError } from '../../api/httpClient';
import {
  NETWORK_ERROR_MESSAGE,
  bookingCommandErrorMessage,
  bookingLoadErrorMessage,
  createBookingErrorMessage,
  loginErrorMessage,
  maintenanceErrorMessage,
  registerErrorMessage,
  rescheduleBookingErrorMessage,
} from '../apiErrors';

describe('loginErrorMessage', () => {
  it('uses one message for wrong credentials, so it never says whether the username exists', () => {
    expect(loginErrorMessage(new ApiError(401, 'x', 'auth.invalid_credentials'))).toBe('El usuario o la contraseña no son correctos.');
  });

  it('explains a locked account and the sign-in rate limit', () => {
    expect(loginErrorMessage(new ApiError(429, 'x', 'auth.account_locked'))).toBe(
      'Demasiados intentos fallidos. Espera unos minutos e inténtalo de nuevo.',
    );
    expect(loginErrorMessage(new ApiError(429, 'x', 'rate_limit.exceeded'))).toBe(
      'Demasiados intentos seguidos. Espera un minuto e inténtalo de nuevo.',
    );
  });

  it('falls back to a generic message and reports connection problems otherwise', () => {
    expect(loginErrorMessage(new ApiError(500, 'x', 'server.error'))).toBe('No se pudo iniciar sesión. Inténtalo de nuevo.');
    expect(loginErrorMessage(new TypeError('Network request failed'))).toBe(NETWORK_ERROR_MESSAGE);
  });
});

describe('registerErrorMessage', () => {
  it('explains a taken username', () => {
    expect(registerErrorMessage(new ApiError(409, 'x', 'user.username_taken'))).toBe('Ese usuario ya existe. Elige otro.');
  });

  it('explains a taken email', () => {
    expect(registerErrorMessage(new ApiError(409, 'x', 'user.email_taken'))).toBe('Ese correo ya está registrado.');
  });

  it('explains each invalid field', () => {
    expect(registerErrorMessage(new ApiError(422, 'x', 'user.invalid_username'))).toContain('3 a 30');
    expect(registerErrorMessage(new ApiError(422, 'x', 'user.invalid_email'))).toBe('El correo no es válido.');
    expect(registerErrorMessage(new ApiError(422, 'x', 'user.invalid_name'))).toBe('El nombre no es válido.');
    expect(registerErrorMessage(new ApiError(422, 'x', 'user.invalid_phone'))).toBe('El teléfono no es válido.');
    expect(registerErrorMessage(new ApiError(422, 'x', 'user.weak_password'))).toContain('8 y 128');
  });

  it('explains that sign-up is closed and the sign-up rate limit', () => {
    expect(registerErrorMessage(new ApiError(403, 'x', 'auth.registration_disabled'))).toBe('Por ahora no se pueden crear cuentas nuevas.');
    expect(registerErrorMessage(new ApiError(429, 'x', 'rate_limit.exceeded'))).toContain('demasiadas cuentas');
  });

  it('falls back to a generic message and reports connection problems otherwise', () => {
    expect(registerErrorMessage(new ApiError(500, 'x', 'server.error'))).toBe('No se pudo crear la cuenta. Inténtalo de nuevo.');
    expect(registerErrorMessage(new TypeError('Network request failed'))).toBe(NETWORK_ERROR_MESSAGE);
  });
});

describe('maintenanceErrorMessage', () => {
  it('explains the rate limit', () => {
    expect(maintenanceErrorMessage(new ApiError(429, 'x', 'rate_limit.exceeded'))).toBe(
      'Ya se ejecutó hace un momento. Espera un minuto e inténtalo de nuevo.',
    );
  });

  it('falls back to a generic message for other API errors and reports connection problems otherwise', () => {
    expect(maintenanceErrorMessage(new ApiError(500, 'x', 'server.error'))).toBe(
      'No se pudo actualizar las reservas. Inténtalo de nuevo.',
    );
    expect(maintenanceErrorMessage(new TypeError('Network request failed'))).toBe(NETWORK_ERROR_MESSAGE);
  });
});

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

describe('bookingLoadErrorMessage', () => {
  it('says the booking does not exist when the API answers 404', () => {
    expect(bookingLoadErrorMessage(new ApiError(404, 'x', 'resource.not_found'))).toBe(
      'No existe una reserva con ese identificador.',
    );
  });

  it('asks to check the connection for any other failure', () => {
    const expected = 'No se pudo cargar la reserva. Revisa la conexión e inténtalo de nuevo.';

    expect(bookingLoadErrorMessage(new ApiError(500, 'x', null))).toBe(expected);
    expect(bookingLoadErrorMessage(new TypeError('Network request failed'))).toBe(expected);
  });
});

describe('bookingCommandErrorMessage', () => {
  it('asks for the reason when the backend says it is missing', () => {
    expect(bookingCommandErrorMessage(new ApiError(422, 'x', 'booking.reason_required'))).toBe('Escribe el motivo.');
  });

  it('explains a guard that is not met yet and an action that is no longer available', () => {
    expect(bookingCommandErrorMessage(new ApiError(409, 'x', 'booking.guard_failed'))).toContain('Todavía no se cumplen');
    expect(bookingCommandErrorMessage(new ApiError(409, 'x', 'booking.invalid_transition'))).toContain('ya no está disponible');
  });

  it('explains that another booking took the slot when reverting the absence', () => {
    expect(bookingCommandErrorMessage(new ApiError(409, 'x', 'booking.slot_unavailable'))).toBe(
      'Otra reserva ya ocupa ese horario, así que no se puede retomar.',
    );
  });

  it('explains a concurrent change and a missing booking', () => {
    expect(bookingCommandErrorMessage(new ApiError(409, 'x', 'concurrency.conflict'))).toContain('cambió');
    expect(bookingCommandErrorMessage(new ApiError(404, 'x', 'resource.not_found'))).toBe('La reserva ya no existe.');
  });

  it('falls back to a generic message and reports connection problems', () => {
    expect(bookingCommandErrorMessage(new ApiError(500, 'x', null))).toBe(
      'No se pudo completar la acción. Inténtalo de nuevo.',
    );
    expect(bookingCommandErrorMessage(new TypeError('Network request failed'))).toBe(NETWORK_ERROR_MESSAGE);
  });
});
