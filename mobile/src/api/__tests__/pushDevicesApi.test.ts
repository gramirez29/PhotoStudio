import { createHttpClient } from '../httpClient';
import { createPushDevicesApi } from '../pushDevicesApi';
import type { FetchFunction } from '../../types/api/http';

/**
 * Creates a fetch stub that answers 204 and records what was requested.
 * @returns The stub and the recorded requests.
 */
function stubNoContent(): { readonly fetchFn: FetchFunction; readonly requests: { url: string; method: string | undefined; body: unknown }[] } {
  const requests: { url: string; method: string | undefined; body: unknown }[] = [];
  const fetchFn: FetchFunction = (input, init) => {
    requests.push({ url: input, method: init?.method, body: typeof init?.body === 'string' ? JSON.parse(init.body) : undefined });
    return Promise.resolve(new Response(null, { status: 204 }));
  };

  return { fetchFn, requests };
}

describe('createPushDevicesApi', () => {
  it('registers the phone with POST and the token and platform in the body', async () => {
    const { fetchFn, requests } = stubNoContent();
    const api = createPushDevicesApi(createHttpClient('https://api.example.test', { fetchFn }));

    await expect(api.register({ token: 'ExponentPushToken[abc]', platform: 'Android' })).resolves.toBeUndefined();

    expect(requests).toEqual([
      {
        url: 'https://api.example.test/api/push-devices',
        method: 'POST',
        body: { token: 'ExponentPushToken[abc]', platform: 'Android' },
      },
    ]);
  });

  it('forgets the phone with POST to the unregister path', async () => {
    const { fetchFn, requests } = stubNoContent();
    const api = createPushDevicesApi(createHttpClient('https://api.example.test', { fetchFn }));

    await expect(api.unregister({ token: 'ExponentPushToken[abc]' })).resolves.toBeUndefined();

    expect(requests).toEqual([
      { url: 'https://api.example.test/api/push-devices/unregister', method: 'POST', body: { token: 'ExponentPushToken[abc]' } },
    ]);
  });
});
