import type { HttpClient } from '../types/api/http';
import type { PushDevicesApi } from '../types/api/pushDevicesApi';

/**
 * Creates the push devices API on top of an HTTP client that sends the access token of the session.
 * @param client HTTP client with session handling.
 * @returns The push devices API.
 */
export function createPushDevicesApi(client: HttpClient): PushDevicesApi {
  return {
    register: (request, signal) => client.post('/api/push-devices', request, () => undefined, signal),
    unregister: (request, signal) => client.post('/api/push-devices/unregister', request, () => undefined, signal),
  };
}
