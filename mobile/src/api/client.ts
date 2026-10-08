import { env } from '../config/env';
import { createBookingsApi } from './bookingsApi';
import { createHealthApi } from './healthApi';
import { createHttpClient } from './httpClient';

/** Shared HTTP client configured with the API URL of the current environment. */
export const httpClient = createHttpClient(env.apiUrl);

/** Booking endpoints. */
export const bookingsApi = createBookingsApi(httpClient);

/** Health endpoints. */
export const healthApi = createHealthApi(httpClient);
