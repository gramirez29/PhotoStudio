import { ResponseShapeError } from '../guards';
import { createHttpClient, ApiError } from '../httpClient';
import { createMaintenanceApi, parseMaintenance } from '../maintenanceApi';
import { jsonResponse, stubFetch } from '../__fixtures__/testResponses';

const maintenancePayload = { bookingsExpired: 2, bookingsSkipped: 1, eventsProcessed: 4, moreWorkPending: false } as const;

describe('parseMaintenance', () => {
  it('parses a valid backend payload', () => {
    expect(parseMaintenance(maintenancePayload)).toEqual(maintenancePayload);
  });

  it('rejects a payload with a missing or mistyped field', () => {
    expect(() => parseMaintenance({ ...maintenancePayload, bookingsExpired: '2' })).toThrow(ResponseShapeError);
    expect(() => parseMaintenance({ ...maintenancePayload, moreWorkPending: 'no' })).toThrow('maintenance.moreWorkPending');
    expect(() => parseMaintenance(null)).toThrow(ResponseShapeError);
  });
});

describe('createMaintenanceApi', () => {
  it('posts to the maintenance endpoint and parses the result', async () => {
    const { fetchFn, urls } = stubFetch(jsonResponse(200, maintenancePayload));
    const api = createMaintenanceApi(createHttpClient('https://api.example.test', { fetchFn }));

    await expect(api.runMaintenance()).resolves.toEqual(maintenancePayload);
    expect(urls).toEqual(['https://api.example.test/api/maintenance/run']);
  });

  it('surfaces the rate limit as an ApiError with the stable code', async () => {
    const problem = { status: 429, title: 'Too many requests.', code: 'rate_limit.exceeded' };
    const { fetchFn } = stubFetch(jsonResponse(429, problem, 'application/problem+json'));
    const api = createMaintenanceApi(createHttpClient('https://api.example.test', { fetchFn }));

    const failure: unknown = await api.runMaintenance().catch((error: unknown) => error);

    expect(failure).toBeInstanceOf(ApiError);
    expect(failure).toMatchObject({ status: 429, code: 'rate_limit.exceeded' });
  });
});
