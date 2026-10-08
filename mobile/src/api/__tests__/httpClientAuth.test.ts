import type { AccessTokenProvider, FetchFunction } from '../../types/api/http';
import { createHttpClient } from '../httpClient';
import { jsonResponse } from '../__fixtures__/testResponses';

/** Parser that returns the body unchanged, for tests that only check transport behavior. */
const identity = (body: unknown): unknown => body;

/** One request the stubbed fetch received. */
type SentRequest = Readonly<{ url: string; authorization: string | undefined }>;

/**
 * Builds a fetch that answers with the given responses in order and records the Authorization header of each request.
 * @param responses Responses to return, one per request.
 * @returns The fetch and the requests it received.
 */
function sequenceFetch(...responses: Response[]): { readonly fetchFn: FetchFunction; readonly sent: SentRequest[] } {
  const sent: SentRequest[] = [];
  const queue = [...responses];
  const fetchFn: FetchFunction = (url, init) => {
    const headers = init?.headers as Record<string, string> | undefined;
    sent.push({ url, authorization: headers?.Authorization });
    const next = queue.shift();
    return Promise.resolve(next ?? jsonResponse(500, {}));
  };

  return { fetchFn, sent };
}

/**
 * Builds a session provider whose behavior each test can set.
 * @param token Token returned for new requests.
 * @param renewed Token returned after a rejection.
 * @returns The provider and its mocks.
 */
function provider(token: string | null, renewed: string | null = null): AccessTokenProvider & {
  readonly getAccessToken: jest.Mock<Promise<string | null>, []>;
  readonly renewAfterRejection: jest.Mock<Promise<string | null>, [string]>;
} {
  return {
    getAccessToken: jest.fn<Promise<string | null>, []>(() => Promise.resolve(token)),
    renewAfterRejection: jest.fn<Promise<string | null>, [string]>(() => Promise.resolve(renewed)),
  };
}

describe('createHttpClient with a session', () => {
  it('sends the access token of the session as a bearer token', async () => {
    const { fetchFn, sent } = sequenceFetch(jsonResponse(200, { ok: true }));
    const client = createHttpClient('https://api.example.test', { fetchFn, auth: provider('token-1') });

    await client.get('/api/bookings', identity);

    expect(sent).toEqual([{ url: 'https://api.example.test/api/bookings', authorization: 'Bearer token-1' }]);
  });

  it('sends no Authorization header when there is no session', async () => {
    const { fetchFn, sent } = sequenceFetch(jsonResponse(200, { ok: true }));
    const client = createHttpClient('https://api.example.test', { fetchFn, auth: provider(null) });

    await client.get('/x', identity);

    expect(sent[0]?.authorization).toBeUndefined();
  });

  it('sends no Authorization header when the client has no session handling', async () => {
    const { fetchFn, sent } = sequenceFetch(jsonResponse(200, { ok: true }));
    const client = createHttpClient('https://api.example.test', { fetchFn });

    await client.post('/api/auth/login', { email: 'a@b.c', password: 'x' }, identity);

    expect(sent[0]?.authorization).toBeUndefined();
  });

  it('renews the session on a 401 and retries the request once with the new token', async () => {
    const { fetchFn, sent } = sequenceFetch(jsonResponse(401, {}), jsonResponse(200, { value: 1 }));
    const auth = provider('old-token', 'new-token');
    const client = createHttpClient('https://api.example.test', { fetchFn, auth });

    const result = await client.get('/api/bookings', identity);

    expect(result).toEqual({ value: 1 });
    expect(auth.renewAfterRejection).toHaveBeenCalledWith('old-token');
    expect(sent.map((request) => request.authorization)).toEqual(['Bearer old-token', 'Bearer new-token']);
  });

  it('resends the same body when it retries a POST', async () => {
    const bodies: string[] = [];
    const responses = [jsonResponse(401, {}), jsonResponse(200, {})];
    const fetchFn: FetchFunction = (_url, init) => {
      bodies.push(String(init?.body));
      return Promise.resolve(responses.shift() ?? jsonResponse(500, {}));
    };
    const client = createHttpClient('https://api.example.test', { fetchFn, auth: provider('old', 'new') });

    await client.post('/api/bookings', { clientName: 'María' }, identity);

    expect(bodies).toEqual(['{"clientName":"María"}', '{"clientName":"María"}']);
  });

  it('gives up after one retry: a second 401 is returned to the caller', async () => {
    const { fetchFn, sent } = sequenceFetch(jsonResponse(401, {}), jsonResponse(401, {}), jsonResponse(200, {}));
    const auth = provider('old-token', 'new-token');
    const client = createHttpClient('https://api.example.test', { fetchFn, auth });

    await expect(client.get('/x', identity)).rejects.toMatchObject({ name: 'ApiError', status: 401 });

    expect(sent).toHaveLength(2);
    expect(auth.renewAfterRejection).toHaveBeenCalledTimes(1);
  });

  it('does not retry when the session could not be renewed', async () => {
    const { fetchFn, sent } = sequenceFetch(jsonResponse(401, {}));
    const client = createHttpClient('https://api.example.test', { fetchFn, auth: provider('old-token', null) });

    await expect(client.get('/x', identity)).rejects.toMatchObject({ status: 401 });

    expect(sent).toHaveLength(1);
  });

  it('does not try to renew a request that was sent without a token', async () => {
    const { fetchFn } = sequenceFetch(jsonResponse(401, {}));
    const auth = provider(null, 'new-token');
    const client = createHttpClient('https://api.example.test', { fetchFn, auth });

    await expect(client.get('/x', identity)).rejects.toMatchObject({ status: 401 });

    expect(auth.renewAfterRejection).not.toHaveBeenCalled();
  });

  it('does not renew the session for errors other than 401', async () => {
    const { fetchFn, sent } = sequenceFetch(jsonResponse(409, { code: 'booking.invalid_transition' }));
    const auth = provider('token-1', 'new-token');
    const client = createHttpClient('https://api.example.test', { fetchFn, auth });

    await expect(client.get('/x', identity)).rejects.toMatchObject({ status: 409 });

    expect(auth.renewAfterRejection).not.toHaveBeenCalled();
    expect(sent).toHaveLength(1);
  });
});
