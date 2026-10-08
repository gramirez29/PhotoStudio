import { ApiError, type HttpClient } from './httpClient';

/** Readiness check of the backend. */
export interface HealthApi {
  /**
   * Asks the backend whether it is ready (API up and MongoDB reachable).
   * @param signal Optional signal to cancel the request.
   * @returns True when ready; false when the backend answers that it is not.
   */
  isReady(signal?: AbortSignal): Promise<boolean>;
}

/**
 * Creates the health API on top of an HTTP client.
 * @param client HTTP client.
 * @returns The health API.
 */
export function createHealthApi(client: HttpClient): HealthApi {
  return {
    isReady: async (signal) => {
      try {
        return await client.get('/health/ready', (body) => body === 'Healthy', signal);
      } catch (error: unknown) {
        // 503 means "reachable but not ready"; any other failure (network, timeout) propagates to the caller.
        if (error instanceof ApiError && error.status === 503) {
          return false;
        }

        throw error;
      }
    },
  };
}
