import { ApiError, createHttpClient, toApiError } from '../httpClient';
import { jsonResponse, stubFetch, textResponse } from '../__fixtures__/testResponses';

/** Parser that returns the body unchanged, for tests that only check transport behavior. */
const identity = (body: unknown): unknown => body;

describe('createHttpClient', () => {
  it('joins the base URL and the path', async () => {
    const { fetchFn, urls } = stubFetch(jsonResponse(200, { ok: true }));
    const client = createHttpClient('https://api.example.test', { fetchFn });

    await client.get('/api/bookings/1', identity);

    expect(urls).toEqual(['https://api.example.test/api/bookings/1']);
  });

  it('parses JSON bodies with the given parser', async () => {
    const { fetchFn } = stubFetch(jsonResponse(200, { value: 42 }));
    const client = createHttpClient('https://api.example.test', { fetchFn });

    const result = await client.get('/x', (body) => (typeof body === 'object' && body !== null ? 'object' : 'other'));

    expect(result).toBe('object');
  });

  it('returns plain text bodies as strings', async () => {
    const { fetchFn } = stubFetch(textResponse(200, 'Healthy'));
    const client = createHttpClient('https://api.example.test', { fetchFn });

    const result = await client.get('/health/ready', identity);

    expect(result).toBe('Healthy');
  });

  it('throws ApiError with the backend code on problem details', async () => {
    const problem = { status: 409, title: 'Conflict', detail: 'Cancel is not allowed.', code: 'booking.invalid_transition' };
    const { fetchFn } = stubFetch(jsonResponse(409, problem, 'application/problem+json'));
    const client = createHttpClient('https://api.example.test', { fetchFn });

    await expect(client.get('/x', identity)).rejects.toMatchObject({
      name: 'ApiError',
      status: 409,
      code: 'booking.invalid_transition',
      message: 'Cancel is not allowed.',
    });
  });
});

describe('toApiError', () => {
  it('falls back to a generic message when the body is not problem details', () => {
    const error = toApiError(500, 'boom');

    expect(error).toBeInstanceOf(ApiError);
    expect(error.code).toBeNull();
    expect(error.message).toBe('Request failed with status 500.');
  });
});
