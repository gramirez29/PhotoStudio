import { ApiError, NetworkError } from '../api/httpClient';
import { ResponseShapeError } from '../api/guards';
import type { LoadErrorView } from '../types/utils/apiErrors.types';

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
