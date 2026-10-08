import { createHealthApi } from '../healthApi';
import { createHttpClient } from '../httpClient';
import { stubFetch, textResponse } from '../__fixtures__/testResponses';

describe('createHealthApi', () => {
  it('reports ready when the backend answers Healthy', async () => {
    const { fetchFn } = stubFetch(textResponse(200, 'Healthy'));
    const api = createHealthApi(createHttpClient('https://api.example.test', { fetchFn }));

    await expect(api.isReady()).resolves.toBe(true);
  });

  it('reports not ready on 503', async () => {
    const { fetchFn } = stubFetch(textResponse(503, 'Unhealthy'));
    const api = createHealthApi(createHttpClient('https://api.example.test', { fetchFn }));

    await expect(api.isReady()).resolves.toBe(false);
  });

  it('propagates network failures', async () => {
    const api = createHealthApi(
      createHttpClient('https://api.example.test', { fetchFn: () => Promise.reject(new TypeError('Network request failed')) }),
    );

    await expect(api.isReady()).rejects.toThrow('Network request failed');
  });
});
