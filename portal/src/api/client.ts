import { apiUrl } from '../config/env';
import { createHttpClient } from './httpClient';
import { createPortalApi } from './portalApi';

/** Portal endpoints, over an HTTP client pointed at the API of the current environment. */
export const portalApi = createPortalApi(createHttpClient(apiUrl));
