import { env } from '../config/env';
import { createBillingApi } from './billingApi';
import { createBookingsApi } from './bookingsApi';
import { createHealthApi } from './healthApi';
import { createHttpClient } from './httpClient';
import { createMaintenanceApi } from './maintenanceApi';
import { createNotificationsApi } from './notificationsApi';
import { sessionTokenProvider } from '../session/sessionManager';

/** Shared HTTP client configured with the API URL of the current environment. It sends the access token of the session. */
export const httpClient = createHttpClient(env.apiUrl, { auth: sessionTokenProvider });

/** Booking endpoints. */
export const bookingsApi = createBookingsApi(httpClient);

/** Health endpoints. */
export const healthApi = createHealthApi(httpClient);

/** Maintenance endpoints (on-demand background work). */
export const maintenanceApi = createMaintenanceApi(httpClient);

/** Billing endpoints (settlements and refunds). */
export const billingApi = createBillingApi(httpClient);

/** Notification inbox endpoints. */
export const notificationsApi = createNotificationsApi(httpClient);
