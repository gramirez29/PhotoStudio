import { ApiError } from '../api/httpClient';

/** Message shown when the request never reached the server or got no answer. */
export const NETWORK_ERROR_MESSAGE = 'No se pudo conectar con el servidor. Revisa la conexión e inténtalo de nuevo.';

/**
 * Converts the error of a failed "load booking" request into a message for the photographer.
 * @param error Error of the query.
 * @returns The message in Spanish.
 */
export function bookingLoadErrorMessage(error: Error): string {
  if (error instanceof ApiError && error.status === 404) {
    return 'No existe una reserva con ese identificador.';
  }

  return 'No se pudo cargar la reserva. Revisa la conexión e inténtalo de nuevo.';
}

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
 * Converts the error of a failed action on a booking (cancel, complete, mark absent, revert) into a message for the photographer.
 * @param error Error of the mutation.
 * @returns The message in Spanish.
 */
export function bookingCommandErrorMessage(error: Error): string {
  if (!(error instanceof ApiError)) {
    return NETWORK_ERROR_MESSAGE;
  }

  switch (error.code) {
    case 'booking.reason_required':
      return 'Escribe el motivo.';
    case 'booking.guard_failed':
      return 'Todavía no se cumplen las condiciones: revisa la hora de la sesión o el plazo de la política.';
    case 'booking.invalid_transition':
      return 'Esa acción ya no está disponible para esta reserva. Vuelve a abrirla para ver su estado actual.';
    case 'booking.slot_unavailable':
      return 'Otra reserva ya ocupa ese horario, así que no se puede retomar.';
    case 'concurrency.conflict':
      return 'La reserva cambió mientras la editabas. Vuelve a abrirla e inténtalo de nuevo.';
    case 'resource.not_found':
      return 'La reserva ya no existe.';
    default:
      return 'No se pudo completar la acción. Inténtalo de nuevo.';
  }
}

/**
 * Converts the error of a failed "run maintenance" request into a message for the photographer.
 * @param error Error of the mutation.
 * @returns The message in Spanish.
 */
export function maintenanceErrorMessage(error: Error): string {
  if (!(error instanceof ApiError)) {
    return NETWORK_ERROR_MESSAGE;
  }

  if (error.code === 'rate_limit.exceeded') {
    return 'Ya se ejecutó hace un momento. Espera un minuto e inténtalo de nuevo.';
  }

  return 'No se pudo actualizar las reservas. Inténtalo de nuevo.';
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
