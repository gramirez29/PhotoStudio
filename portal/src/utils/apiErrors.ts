import { ApiError, NetworkError } from '../api/httpClient';
import { ResponseShapeError } from '../api/guards';
import type { ActionErrorView, LoadErrorView } from '../types/utils/apiErrors.types';

/**
 * Explains to the client why the booking could not be loaded.
 * @param error Error of the request.
 * @returns The title, the explanation and whether trying again can help.
 */
export function describeLoadError(error: unknown): LoadErrorView {
  if (error instanceof ApiError && error.status === 404) {
    return {
      title: 'Este enlace no es válido',
      message: 'El enlace no existe o ya venció. Pídele a tu fotógrafo que te envíe uno nuevo.',
      canRetry: false,
    };
  }

  if (error instanceof ApiError && error.status === 429) {
    return {
      title: 'Demasiadas consultas',
      message: 'Espera un momento e inténtalo de nuevo.',
      canRetry: true,
    };
  }

  if (error instanceof NetworkError) {
    return {
      title: 'Sin conexión',
      message: 'No pudimos conectarnos. Revisa tu internet e inténtalo de nuevo.',
      canRetry: true,
    };
  }

  if (error instanceof ResponseShapeError) {
    return {
      title: 'No pudimos mostrar tu reserva',
      message: 'La página y el servidor no se entendieron. Avísale a tu fotógrafo si el problema sigue.',
      canRetry: true,
    };
  }

  return {
    title: 'Algo salió mal',
    message: 'No pudimos cargar tu reserva. Inténtalo de nuevo en un momento.',
    canRetry: true,
  };
}

/** Message for a request that never got an answer. */
const NO_CONNECTION = 'No pudimos conectarnos. Revisa tu internet e inténtalo de nuevo.';

/** Message for a link that stopped being valid while the client was using it. */
const LINK_NO_LONGER_VALID = 'El enlace ya no es válido. Pídele a tu fotógrafo que te envíe uno nuevo.';

/**
 * Builds the view of an error that has no special meaning for the action.
 * @param error Error of the request.
 * @param fallback Message for an error nobody planned for.
 * @returns The view.
 */
function commonActionError(error: unknown, fallback: string): ActionErrorView {
  if (error instanceof NetworkError) {
    return { message: NO_CONNECTION, retryAfterSeconds: null, sessionExpired: false };
  }

  if (error instanceof ApiError && error.status === 404) {
    return { message: LINK_NO_LONGER_VALID, retryAfterSeconds: null, sessionExpired: false };
  }

  if (error instanceof ApiError && error.code === 'rate_limit.exceeded') {
    return {
      message: 'Demasiados intentos seguidos. Espera un minuto e inténtalo de nuevo.',
      retryAfterSeconds: error.retryAfterSeconds,
      sessionExpired: false,
    };
  }

  return { message: fallback, retryAfterSeconds: null, sessionExpired: false };
}

/**
 * Explains why the verification code could not be sent.
 * @param error Error of the request.
 * @returns The message and, when the server said how long to wait, the seconds.
 */
export function describeCodeRequestError(error: unknown): ActionErrorView {
  if (error instanceof ApiError && error.code === 'portal.email_missing') {
    return {
      message: 'Tu reserva no tiene un correo registrado. Pídele a tu fotógrafo que lo agregue.',
      retryAfterSeconds: null,
      sessionExpired: false,
    };
  }

  if (error instanceof ApiError && error.code === 'portal.otp_rate_limited') {
    return {
      message: 'Ya te enviamos un código hace poco. Revisa tu correo o espera para pedir otro.',
      retryAfterSeconds: error.retryAfterSeconds,
      sessionExpired: false,
    };
  }

  if (error instanceof ApiError && error.code === 'email.delivery_failed') {
    return { message: 'No pudimos enviar el correo ahora. Inténtalo en un momento.', retryAfterSeconds: null, sessionExpired: false };
  }

  return commonActionError(error, 'No pudimos enviar el código. Inténtalo de nuevo.');
}

/**
 * Explains why the verification code was not accepted.
 * @param error Error of the request.
 * @returns The message.
 */
export function describeVerifyError(error: unknown): ActionErrorView {
  if (error instanceof ApiError && error.code === 'portal.invalid_code') {
    return {
      message: 'El código no es correcto o ya venció. Revísalo en tu correo o pide uno nuevo.',
      retryAfterSeconds: null,
      sessionExpired: false,
    };
  }

  return commonActionError(error, 'No pudimos verificar el código. Inténtalo de nuevo.');
}

/**
 * Explains why the payment proof was not registered.
 * @param error Error of the request.
 * @returns The message, and whether the verification expired.
 */
export function describeSubmitError(error: unknown): ActionErrorView {
  if (error instanceof ApiError && error.status === 401) {
    return {
      message: 'Tu verificación venció. Confirma tu correo otra vez; lo que escribiste se conserva.',
      retryAfterSeconds: null,
      sessionExpired: true,
    };
  }

  if (error instanceof ApiError && error.code === 'money.invalid_amount') {
    return {
      message: 'El monto es mayor a lo que falta por pagar. Revisa la reserva y corrígelo.',
      retryAfterSeconds: null,
      sessionExpired: false,
    };
  }

  if (error instanceof ApiError && (error.code === 'booking.invalid_transition' || error.code === 'booking.guard_failed')) {
    return { message: 'Esta reserva ya no recibe pagos. Escríbele a tu fotógrafo.', retryAfterSeconds: null, sessionExpired: false };
  }

  if (error instanceof ApiError && error.code === 'concurrency.conflict') {
    return { message: 'La reserva cambió mientras enviabas. Inténtalo de nuevo.', retryAfterSeconds: null, sessionExpired: false };
  }

  return commonActionError(error, 'No pudimos registrar tu comprobante. Inténtalo de nuevo.');
}

/** Message for a verification that expired while the client was on the contract. */
const CONTRACT_SESSION_EXPIRED = 'Tu verificación venció. Confirma tu correo otra vez; lo que escribiste se conserva.';

/**
 * Explains why the contract could not be loaded.
 * @param error Error of the request.
 * @returns The message, and whether the verification expired.
 */
export function describeContractLoadError(error: unknown): ActionErrorView {
  if (error instanceof ApiError && error.status === 401) {
    return { message: CONTRACT_SESSION_EXPIRED, retryAfterSeconds: null, sessionExpired: true };
  }

  return commonActionError(error, 'No pudimos cargar el contrato. Inténtalo de nuevo.');
}

/**
 * Explains why the contract was not signed.
 * @param error Error of the request.
 * @returns The message, and whether the verification expired.
 */
export function describeSignContractError(error: unknown): ActionErrorView {
  if (error instanceof ApiError && error.status === 401) {
    return { message: CONTRACT_SESSION_EXPIRED, retryAfterSeconds: null, sessionExpired: true };
  }

  if (error instanceof ApiError && error.code === 'contract.template_changed') {
    return {
      message: 'El contrato cambió mientras lo leías. Ciérralo y ábrelo otra vez para leer la versión vigente.',
      retryAfterSeconds: null,
      sessionExpired: false,
    };
  }

  if (error instanceof ApiError && error.code === 'booking.contract_already_signed') {
    return { message: 'Este contrato ya está firmado.', retryAfterSeconds: null, sessionExpired: false };
  }

  if (error instanceof ApiError && error.code === 'contract.invalid_id_number') {
    return { message: 'La cédula o el DIMEX no es válido. Revísalo y corrígelo.', retryAfterSeconds: null, sessionExpired: false };
  }

  if (error instanceof ApiError && error.code === 'contract.invalid_details') {
    return { message: 'Revisa el número de personas y el lugar de la sesión.', retryAfterSeconds: null, sessionExpired: false };
  }

  if (error instanceof ApiError && (error.code === 'booking.invalid_transition' || error.code === 'booking.guard_failed')) {
    return { message: 'Esta reserva ya no se puede firmar. Escríbele a tu fotógrafo.', retryAfterSeconds: null, sessionExpired: false };
  }

  if (error instanceof ApiError && error.code === 'concurrency.conflict') {
    return { message: 'La reserva cambió mientras firmabas. Inténtalo de nuevo.', retryAfterSeconds: null, sessionExpired: false };
  }

  return commonActionError(error, 'No pudimos registrar tu firma. Inténtalo de nuevo.');
}
