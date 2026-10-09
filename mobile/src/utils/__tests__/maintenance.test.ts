import type { MaintenanceResponse } from '../../types/api/maintenance';
import { describeMaintenanceResult } from '../maintenance';

/**
 * Builds a maintenance result, with nothing done unless overridden.
 * @param overrides Values to replace.
 * @returns The result.
 */
function result(overrides: Partial<MaintenanceResponse> = {}): MaintenanceResponse {
  return { bookingsExpired: 0, bookingsSkipped: 0, eventsProcessed: 0, notificationsDelivered: 0, moreWorkPending: false, ...overrides };
}

describe('describeMaintenanceResult', () => {
  it('says there was nothing to do', () => {
    expect(describeMaintenanceResult(result())).toBe('No había reservas vencidas.');
  });

  it('uses the singular for one expired booking and the plural for several', () => {
    expect(describeMaintenanceResult(result({ bookingsExpired: 1 }))).toBe('1 reserva vencida liberada.');
    expect(describeMaintenanceResult(result({ bookingsExpired: 3 }))).toBe('3 reservas vencidas liberadas.');
  });

  it('mentions the bookings that could not be updated', () => {
    expect(describeMaintenanceResult(result({ bookingsExpired: 2, bookingsSkipped: 1 }))).toBe(
      '2 reservas vencidas liberadas. 1 no se pudo actualizar; inténtalo de nuevo en un momento.',
    );
    expect(describeMaintenanceResult(result({ bookingsSkipped: 2 }))).toContain('2 no se pudieron actualizar');
  });

  it('mentions the notifications delivered', () => {
    expect(describeMaintenanceResult(result({ notificationsDelivered: 1 }))).toBe('No había reservas vencidas. 1 aviso nuevo en tu bandeja.');
    expect(describeMaintenanceResult(result({ notificationsDelivered: 3 }))).toContain('3 avisos nuevos');
  });

  it('asks to run it again when the pass stopped at its limit', () => {
    expect(describeMaintenanceResult(result({ bookingsExpired: 1000, moreWorkPending: true }))).toBe(
      '1000 reservas vencidas liberadas. Quedan más por procesar: vuelve a ejecutarlo en un minuto.',
    );
  });
});
