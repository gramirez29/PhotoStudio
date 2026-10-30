import type { ApiHealthDescription } from '../types/utils/apiHealth.types';

/**
 * Describes the readiness check of the backend for the photographer.
 * @param isError True when the check could not reach the server.
 * @param ready True when the API and the database are ready, false when the API answers that they are not, undefined while loading.
 * @returns The sentence and the role of the palette to color it with.
 */
export function describeApiHealth(isError: boolean, ready: boolean | undefined): ApiHealthDescription {
  if (isError) {
    return { label: 'Sin conexión con el servidor', color: 'error' };
  }

  if (ready === true) {
    return { label: 'Servidor conectado', color: 'success' };
  }

  if (ready === false) {
    return { label: 'Servidor disponible, base de datos sin conexión', color: 'warning' };
  }

  return { label: 'Verificando conexión con el servidor…', color: 'onSurfaceVariant' };
}
