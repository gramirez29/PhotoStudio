import { env } from '../config/env';
import { createBookingsApi } from './bookingsApi';
import { createHealthApi } from './healthApi';
import { createHttpClient } from './httpClient';
import { createMaintenanceApi } from './maintenanceApi';

/** Shared HTTP client configured with the API URL of the current environment. */
export const httpClient = createHttpClient(env.apiUrl);

/** Booking endpoints. */
export const bookingsApi = createBookingsApi(httpClient);

/** Health endpoints. */
export const healthApi = createHealthApi(httpClient);

/** Maintenance endpoints (on-demand background work). */
export const maintenanceApi = createMaintenanceApi(httpClient);
