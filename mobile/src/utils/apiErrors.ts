import { ApiError } from '../api/httpClient';

/** Message shown when the request never reached the server or got no answer. */
export const NETWORK_ERROR_MESSAGE = 'No se pudo conectar con el servidor. Revisa la conexión e inténtalo de nuevo.';

/**
 * Converts the error of a failed "create booking" request into a message for the photographer.
 * @param error Error of the mutation.
 * @returns The message in Spanish.
 */
export function createBookingErrorMessage(error: Error): string {
  if (!(error instanceof ApiError)) {
    return NETWORK_ERROR_MESSAGE;
  }

  switch (error.code) {
    case 'booking.slot_unavailable':
      return 'Ya tienes una reserva en ese horario. Elige otra hora.';
    case 'booking.session_in_past':
      return 'La sesión debe empezar en el futuro.';
    case 'schedule.invalid_time_slot':
      return 'El horario de la sesión no es válido.';
    case 'money.invalid_amount':
      return 'El precio no es válido.';
    case 'domain.required_value':
      return 'Faltan datos obligatorios.';
    default:
      return 'No se pudo crear la reserva. Inténtalo de nuevo.';
  }
}

/**
 * Converts the error of a failed "reschedule booking" request into a message for the photographer.
 * @param error Error of the mutation.
 * @returns The message in Spanish.
 */
export function rescheduleBookingErrorMessage(error: Error): string {
  if (!(error instanceof ApiError)) {
    return NETWORK_ERROR_MESSAGE;
  }

  switch (error.code) {
    case 'booking.slot_unavailable':
      return 'Ya tienes otra reserva en ese horario. Elige otra hora.';
    case 'booking.invalid_transition':
      return 'Solo se pueden reprogramar las reservas confirmadas.';
    case 'booking.session_in_past':
      return 'La sesión debe empezar en el futuro.';
    case 'schedule.invalid_time_slot':
      return 'El horario de la sesión no es válido.';
    case 'concurrency.conflict':
      return 'La reserva cambió mientras la editabas. Vuelve a abrirla e inténtalo de nuevo.';
    case 'resource.not_found':
      return 'La reserva ya no existe.';
    default:
      return 'No se pudo reprogramar la reserva. Inténtalo de nuevo.';
  }
}
