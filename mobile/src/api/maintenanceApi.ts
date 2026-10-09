import type { MaintenanceResponse } from '../types/api/maintenance';
import type { MaintenanceApi } from '../types/api/maintenanceApi';
import type { HttpClient } from '../types/api/http';
import { expectRecord, readBoolean, readNumber } from './guards';

/**
 * Parses the result of a maintenance pass, validating every field against the backend contract.
 * @param value Unknown response body.
 * @returns The typed result.
 */
export function parseMaintenance(value: unknown): MaintenanceResponse {
  const path = 'maintenance';
  const record = expectRecord(value, path);
  return {
    bookingsExpired: readNumber(record, 'bookingsExpired', path),
    bookingsSkipped: readNumber(record, 'bookingsSkipped', path),
    eventsProcessed: readNumber(record, 'eventsProcessed', path),
    notificationsDelivered: readNumber(record, 'notificationsDelivered', path),
    moreWorkPending: readBoolean(record, 'moreWorkPending', path),
  };
}

/**
 * Creates the maintenance API on top of an HTTP client.
 * @param client HTTP client.
 * @returns The maintenance API.
 */
export function createMaintenanceApi(client: HttpClient): MaintenanceApi {
  return {
    runMaintenance: (signal) => client.post('/api/maintenance/run', undefined, parseMaintenance, signal),
  };
}
