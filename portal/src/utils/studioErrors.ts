import { ApiError, NetworkError } from '../api/httpClient';
import { ResponseShapeError } from '../api/guards';
import type { ActionErrorView } from '../types/utils/apiErrors.types';

/** Message for a request that never got an answer. */
const NO_CONNECTION = 'No pudimos conectarnos. Revisa tu internet e inténtalo de nuevo.';

/** Message for an answer the page did not expect. */
const UNEXPECTED = 'Algo salió mal. Inténtalo de nuevo en un momento.';

/**
 * Builds the view of an error with the message the page shows.
 * @param message What went wrong and what to do.
 * @param retryAfterSeconds Seconds to wait before trying again, when the server said so.
 * @returns The view.
 */
function view(message: string, retryAfterSeconds: number | null = null): ActionErrorView {
  return { message, retryAfterSeconds, sessionExpired: false };
}

/**
 * Words a wait the server asked for, in the unit that reads best.
 * @param seconds Seconds to wait.
 * @returns For example "45 segundos" or "15 minutos".
 */
function describeWait(seconds: number): string {
  if (seconds < 90) {
    return `${Math.max(1, Math.ceil(seconds))} segundos`;
  }

  return `${Math.ceil(seconds / 60)} minutos`;
}

/**
 * Explains why the password step failed. A wrong password and an unknown user get the same words on purpose: the server does not
 * say which one it was, and neither does the page.
 * @param error Error of the request.
 * @returns The message and, when the server said how long to wait, the seconds.
 */
export function describeLoginError(error: unknown): ActionErrorView {
  if (error instanceof NetworkError) {
    return view(NO_CONNECTION);
  }

  if (error instanceof ApiError) {
    if (error.status === 401) {
      return view('El usuario o la contraseña no son correctos.');
    }

    if (error.code === 'auth.account_locked') {
      return view(
        error.retryAfterSeconds === null
          ? 'Hubo demasiados intentos fallidos. Espera un rato e inténtalo de nuevo.'
          : `Hubo demasiados intentos fallidos. Inténtalo de nuevo en ${describeWait(error.retryAfterSeconds)}.`,
        error.retryAfterSeconds,
      );
    }

    if (error.code === 'auth.login_code_rate_limited') {
      return view(
        error.retryAfterSeconds === null
          ? 'Ya te enviamos un código hace un momento. Revisa tu correo y espera antes de pedir otro.'
          : `Ya te enviamos un código hace un momento. Revisa tu correo, o inténtalo de nuevo en ${describeWait(error.retryAfterSeconds)}.`,
        error.retryAfterSeconds,
      );
    }

    if (error.code === 'rate_limit.exceeded') {
      return view('Demasiados intentos seguidos. Espera un minuto e inténtalo de nuevo.', error.retryAfterSeconds);
    }

    if (error.code === 'auth.web_requires_verified_email') {
      return view('Para entrar aquí, primero verifica el correo de tu cuenta desde la app.');
    }

    if (error.code === 'request.forbidden_origin') {
      return view('Esta página no está autorizada para entrar desde esta dirección. Abre el sitio desde su dirección oficial.');
    }

    if (error.code === 'email.delivery_failed') {
      return view('No pudimos enviar el código a tu correo. Inténtalo de nuevo en un momento.');
    }
  }

  return error instanceof ResponseShapeError ? view('La página y el servidor no se entendieron. Inténtalo de nuevo más tarde.') : view(UNEXPECTED);
}

/**
 * Explains why the code could not be checked or sent again.
 * @param error Error of the request.
 * @returns The message and, when the server said how long to wait, the seconds.
 */
export function describeCodeError(error: unknown): ActionErrorView {
  if (error instanceof NetworkError) {
    return view(NO_CONNECTION);
  }

  if (error instanceof ApiError) {
    if (error.code === 'auth.invalid_login_code') {
      return view('El código no es válido o ya venció. Pide uno nuevo, o vuelve a empezar.');
    }

    if (error.code === 'auth.login_code_rate_limited') {
      return view(
        error.retryAfterSeconds === null
          ? 'Ya te enviamos un código hace un momento. Espera antes de pedir otro.'
          : `Ya te enviamos un código hace un momento. Puedes pedir otro en ${describeWait(error.retryAfterSeconds)}.`,
        error.retryAfterSeconds,
      );
    }

    if (error.code === 'rate_limit.exceeded') {
      return view('Demasiados intentos seguidos. Espera un minuto e inténtalo de nuevo.', error.retryAfterSeconds);
    }

    if (error.code === 'email.delivery_failed') {
      return view('No pudimos enviar el código a tu correo. Inténtalo de nuevo en un momento.');
    }
  }

  return view(UNEXPECTED);
}
