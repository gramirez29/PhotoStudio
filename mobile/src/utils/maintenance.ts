import type { MaintenanceResponse } from '../types/api/maintenance';

/**
 * Describes, for the photographer, what a maintenance pass did.
 * @param result Result reported by the backend.
 * @returns The message in Spanish.
 */
export function describeMaintenanceResult(result: MaintenanceResponse): string {
  const parts: string[] = [];

  if (result.bookingsExpired === 0) {
    parts.push('No había reservas vencidas.');
  } else if (result.bookingsExpired === 1) {
    parts.push('1 reserva vencida liberada.');
  } else {
    parts.push(`${result.bookingsExpired} reservas vencidas liberadas.`);
  }

  if (result.bookingsSkipped === 1) {
    parts.push('1 no se pudo actualizar; inténtalo de nuevo en un momento.');
  } else if (result.bookingsSkipped > 1) {
    parts.push(`${result.bookingsSkipped} no se pudieron actualizar; inténtalo de nuevo en un momento.`);
  }

  if (result.moreWorkPending) {
    parts.push('Quedan más por procesar: vuelve a ejecutarlo en un minuto.');
  }

  return parts.join(' ');
}
