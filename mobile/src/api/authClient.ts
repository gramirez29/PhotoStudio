import { env } from '../config/env';
import { createAuthApi } from './authApi';
import { createHttpClient } from './httpClient';

/** HTTP client without session handling, for the endpoints that create and renew the session. */
export const authHttpClient = createHttpClient(env.apiUrl);

/** Sign-in, session renewal and sign-out endpoints. */
export const authApi = createAuthApi(authHttpClient);
