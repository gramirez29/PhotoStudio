import type { MaintenanceResponse } from './maintenance';

/** Maintenance endpoints used by the photographer app. */
export interface MaintenanceApi {
  /**
   * Runs one maintenance pass on demand: expires the tentative bookings whose hold ended and delivers pending events.
   * The backend allows one call per minute.
   * @param signal Optional signal to cancel the request.
   * @returns What the pass did.
   */
  runMaintenance(signal?: AbortSignal): Promise<MaintenanceResponse>;
}
