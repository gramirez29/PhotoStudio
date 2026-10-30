import { apiUrl } from '../config/env';
import { createHttpClient } from './httpClient';
import { createPortalApi } from './portalApi';
import { createStudioApi } from './studioApi';

/** Portal endpoints, over an HTTP client pointed at the API of the current environment. */
export const portalApi = createPortalApi(createHttpClient(apiUrl));

/** Endpoints of the web area of the photographer. Its client sends the cookies of the API: that session is a cookie, not a token. */
export const studioApi = createStudioApi(createHttpClient(apiUrl, { withCredentials: true }));
