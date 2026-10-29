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
    case 'payment.invalid_status':
      return 'Ese comprobante ya fue verificado o rechazado. Vuelve a abrir la reserva para ver su estado.';
    case 'payment.not_found':
      return 'Ese comprobante ya no existe. Vuelve a abrir la reserva.';
    case 'booking.contract_already_signed':
      return 'El contrato de esta reserva ya está firmado.';
    case 'contract.template_changed':
      return 'El contrato cambió mientras se leía. Cierra esta pantalla y ábrela otra vez para leer la versión vigente.';
    case 'contract.invalid_id_number':
      return 'La cédula o el DIMEX no es válido. Revísalo y corrígelo.';
    case 'contract.invalid_details':
      return 'Revisa el número de personas y el lugar de la sesión.';
    case 'money.invalid_amount':
    case 'money.invalid_currency':
    case 'money.currency_mismatch':
      return 'El monto no es válido para esta reserva. Revísalo e inténtalo de nuevo.';
    case 'payment.invalid_method':
      return 'Ese método de pago no se puede registrar en persona.';
    case 'domain.required_value':
      return 'Faltan datos obligatorios. Revísalos e inténtalo de nuevo.';
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
 * Converts the error of a failed sign-in into a message for the user. A wrong username and a wrong password get the same
 * message on purpose, like the backend.
 * @param error Error of the mutation.
 * @returns The message in Spanish.
 */
export function loginErrorMessage(error: Error): string {
  if (!(error instanceof ApiError)) {
    return NETWORK_ERROR_MESSAGE;
  }

  switch (error.code) {
    case 'auth.invalid_credentials':
      return 'El usuario o la contraseña no son correctos.';
    case 'auth.account_locked':
      return 'Demasiados intentos fallidos. Espera unos minutos e inténtalo de nuevo.';
    case 'rate_limit.exceeded':
      return 'Demasiados intentos seguidos. Espera un minuto e inténtalo de nuevo.';
    default:
      return 'No se pudo iniciar sesión. Inténtalo de nuevo.';
  }
}

/**
 * Converts the error of a failed "change password" request into a message for the user.
 * @param error Error of the mutation.
 * @returns The message in Spanish.
 */
export function changePasswordErrorMessage(error: Error): string {
  if (!(error instanceof ApiError)) {
    return NETWORK_ERROR_MESSAGE;
  }

  switch (error.code) {
    case 'user.invalid_current_password':
      return 'La contraseña actual no es correcta.';
    case 'user.weak_password':
      return 'La contraseña nueva debe tener entre 8 y 128 caracteres y ser distinta de la actual.';
    case 'auth.account_locked':
      return 'Demasiados intentos fallidos. Espera unos minutos e inténtalo de nuevo.';
    case 'rate_limit.exceeded':
      return 'Demasiados intentos seguidos. Espera un minuto e inténtalo de nuevo.';
    case 'concurrency.conflict':
      return 'La contraseña cambió mientras la editabas. Inténtalo de nuevo.';
    default:
      return 'No se pudo cambiar la contraseña. Inténtalo de nuevo.';
  }
}

/**
 * Converts the error of a failed account creation into a message for the user.
 * @param error Error of the mutation.
 * @returns The message in Spanish.
 */
export function registerErrorMessage(error: Error): string {
  if (!(error instanceof ApiError)) {
    return NETWORK_ERROR_MESSAGE;
  }

  switch (error.code) {
    case 'user.username_taken':
      return 'Ese usuario ya existe. Elige otro.';
    case 'user.invalid_username':
      return 'El usuario no es válido: usa de 3 a 30 letras, números, puntos, guiones o guiones bajos.';
    case 'user.email_taken':
      return 'Ese correo ya está registrado.';
    case 'user.invalid_email':
      return 'El correo no es válido.';
    case 'user.invalid_name':
      return 'El nombre no es válido.';
    case 'user.invalid_phone':
      return 'El teléfono no es válido.';
    case 'user.weak_password':
      return 'La contraseña debe tener entre 8 y 128 caracteres.';
    case 'auth.registration_disabled':
      return 'Por ahora no se pueden crear cuentas nuevas.';
    case 'rate_limit.exceeded':
      return 'Se crearon demasiadas cuentas desde este dispositivo. Inténtalo más tarde.';
    default:
      return 'No se pudo crear la cuenta. Inténtalo de nuevo.';
  }
}

/**
 * Converts the error of a failed "complete refund" request into a message for the photographer.
 * @param error Error of the mutation.
 * @returns The message in Spanish.
 */
export function refundErrorMessage(error: Error): string {
  if (!(error instanceof ApiError)) {
    return NETWORK_ERROR_MESSAGE;
  }

  switch (error.code) {
    case 'booking.invalid_transition':
      return 'Este reembolso ya no está pendiente. Vuelve a la lista para ver su estado actual.';
    case 'payment.invalid_method':
      return 'El reembolso solo se puede registrar en efectivo o por SINPE Móvil.';
    case 'domain.required_value':
      return 'La nota es demasiado larga.';
    case 'concurrency.conflict':
      return 'El reembolso cambió mientras lo registrabas. Vuelve a la lista e inténtalo de nuevo.';
    case 'resource.not_found':
      return 'El reembolso ya no existe.';
    default:
      return 'No se pudo registrar el reembolso. Inténtalo de nuevo.';
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

/**
 * Converts the error of a failed email verification into a message for the user.
 * @param error Error of the mutation.
 * @returns The message in Spanish.
 */
export function verifyEmailErrorMessage(error: Error): string {
  if (!(error instanceof ApiError)) {
    return NETWORK_ERROR_MESSAGE;
  }

  switch (error.code) {
    case 'auth.invalid_email_code':
      return 'El código no es correcto o ya venció. Revísalo en tu correo o pide uno nuevo.';
    case 'rate_limit.exceeded':
      return 'Demasiados intentos seguidos. Espera un minuto e inténtalo de nuevo.';
    default:
      return 'No se pudo verificar el correo. Inténtalo de nuevo.';
  }
}

/**
 * Converts the error of a failed request for a new verification code into a message for the user.
 * @param error Error of the mutation.
 * @returns The message in Spanish.
 */
export function resendEmailCodeErrorMessage(error: Error): string {
  if (!(error instanceof ApiError)) {
    return NETWORK_ERROR_MESSAGE;
  }

  switch (error.code) {
    case 'auth.email_code_rate_limited':
      return 'Ya te enviamos un código hace poco. Revisa tu correo, o espera un momento para pedir otro.';
    case 'auth.email_already_verified':
      return 'Tu correo ya está verificado. Cierra la sesión y vuelve a entrar.';
    case 'auth.email_missing':
      return 'Tu cuenta no tiene un correo registrado.';
    case 'email.delivery_failed':
      return 'No pudimos enviar el correo ahora. Inténtalo en un momento.';
    case 'rate_limit.exceeded':
      return 'Demasiados intentos seguidos. Espera un minuto e inténtalo de nuevo.';
    default:
      return 'No se pudo enviar el código. Inténtalo de nuevo.';
  }
}

/**
 * Converts the error of a failed correction of a booking into a message for the user. It words the refusals of this action (the
 * package or the price can no longer change, an invalid email) instead of the generic ones of the other actions.
 * @param error Error of the mutation.
 * @returns The message in Spanish.
 */
export function editBookingErrorMessage(error: Error): string {
  if (!(error instanceof ApiError)) {
    return NETWORK_ERROR_MESSAGE;
  }

  switch (error.code) {
    case 'booking.guard_failed':
      return 'El paquete y el precio ya no se pueden cambiar: el contrato está firmado o hay pagos registrados.';
    case 'booking.invalid_transition':
      return 'Esta reserva ya no se puede editar. Vuelve a abrirla para ver su estado actual.';
    case 'user.invalid_email':
      return 'El correo no es válido. Escríbelo como nombre@correo.com o déjalo vacío.';
    case 'domain.required_value':
      return 'Faltan datos obligatorios. Revísalos e inténtalo de nuevo.';
    case 'money.invalid_amount':
      return 'El precio no es válido.';
    default:
      return bookingCommandErrorMessage(error);
  }
}
