import { createAuthApi, parseAuthSession } from '../authApi';
import { ResponseShapeError } from '../guards';
import { ApiError, createHttpClient } from '../httpClient';
import { jsonResponse, stubFetch } from '../__fixtures__/testResponses';

const sessionPayload = {
  accessToken: 'access.jwt.token',
  accessTokenExpiresAt: '2026-10-08T21:15:00+00:00',
  refreshToken: 'refresh-secret',
  refreshTokenExpiresAt: '2026-11-07T21:00:00+00:00',
  photographerId: '0199a1b2-0000-7000-8000-000000000002',
  username: 'ana',
  email: 'ana@example.com',
  name: 'Ana Pérez',
} as const;

describe('parseAuthSession', () => {
  it('parses a valid backend payload', () => {
    expect(parseAuthSession(sessionPayload)).toEqual(sessionPayload);
  });

  it('rejects a payload with a missing or mistyped field', () => {
    expect(() => parseAuthSession({ ...sessionPayload, accessToken: 7 })).toThrow(ResponseShapeError);
    expect(() => parseAuthSession({ ...sessionPayload, refreshToken: undefined })).toThrow('session.refreshToken');
    expect(() => parseAuthSession(null)).toThrow(ResponseShapeError);
  });
});

describe('createAuthApi', () => {
  it('posts the credentials to the login endpoint and parses the session', async () => {
    const requests: { readonly url: string; readonly body: unknown }[] = [];
    const client = createHttpClient('https://api.example.test', {
      fetchFn: (url, init) => {
        requests.push({ url, body: JSON.parse(String(init?.body)) });
        return Promise.resolve(jsonResponse(200, sessionPayload));
      },
    });

    const session = await createAuthApi(client).login('ana', 'secret password');

    expect(requests).toEqual([
      { url: 'https://api.example.test/api/auth/login', body: { username: 'ana', password: 'secret password' } },
    ]);
    expect(session.photographerId).toBe(sessionPayload.photographerId);
  });

  it('posts the new account to the register endpoint and parses the session', async () => {
    const requests: { readonly url: string; readonly body: unknown }[] = [];
    const client = createHttpClient('https://api.example.test', {
      fetchFn: (url, init) => {
        requests.push({ url, body: JSON.parse(String(init?.body)) });
        return Promise.resolve(jsonResponse(201, sessionPayload));
      },
    });
    const request = { username: 'ana', email: 'ana@example.com', password: 'a long password', name: 'Ana Pérez', phone: '+50670189220' };

    const session = await createAuthApi(client).register(request);

    expect(requests).toEqual([{ url: 'https://api.example.test/api/auth/register', body: request }]);
    expect(session.name).toBe('Ana Pérez');
  });

  it('surfaces the stable error code when the username is taken', async () => {
    const problem = { status: 409, title: 'Conflict', code: 'user.username_taken' };
    const { fetchFn } = stubFetch(jsonResponse(409, problem, 'application/problem+json'));
    const client = createHttpClient('https://api.example.test', { fetchFn });

    const failure: unknown = await createAuthApi(client)
      .register({ username: 'ana', email: 'ana@example.com', password: 'a long password', name: 'Ana', phone: '+50670189220' })
      .catch((error: unknown) => error);

    expect(failure).toMatchObject({ status: 409, code: 'user.username_taken' });
  });

  it('posts the refresh token to the refresh endpoint', async () => {
    const { fetchFn, urls } = stubFetch(jsonResponse(200, sessionPayload));
    const client = createHttpClient('https://api.example.test', { fetchFn });

    const session = await createAuthApi(client).refresh('old-refresh');

    expect(urls).toEqual(['https://api.example.test/api/auth/refresh']);
    expect(session.refreshToken).toBe('refresh-secret');
  });

  it('accepts the empty 204 answer of the logout endpoint', async () => {
    const { fetchFn, urls } = stubFetch(new Response(null, { status: 204 }));
    const client = createHttpClient('https://api.example.test', { fetchFn });

    await expect(createAuthApi(client).logout('refresh-secret')).resolves.toBeUndefined();

    expect(urls).toEqual(['https://api.example.test/api/auth/logout']);
  });

  it('surfaces the stable error code when the credentials are refused', async () => {
    const problem = { status: 401, title: 'Authentication failed.', code: 'auth.invalid_credentials' };
    const { fetchFn } = stubFetch(jsonResponse(401, problem, 'application/problem+json'));
    const client = createHttpClient('https://api.example.test', { fetchFn });

    const failure: unknown = await createAuthApi(client).login('a@b.c', 'x').catch((error: unknown) => error);

    expect(failure).toBeInstanceOf(ApiError);
    expect(failure).toMatchObject({ status: 401, code: 'auth.invalid_credentials' });
  });
});
