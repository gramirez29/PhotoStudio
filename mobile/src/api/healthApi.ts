import type { HealthApi } from '../types/api/healthApi';
import type { HttpClient } from '../types/api/http';
import { ApiError } from './httpClient';

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
