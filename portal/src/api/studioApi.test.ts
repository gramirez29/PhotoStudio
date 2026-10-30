import { describe, expect, it, vi } from 'vitest';
import { galleryPayload, jsonResponse, studioUserPayload } from '../test/fixtures';
import { ApiError, createHttpClient } from './httpClient';
import { ResponseShapeError } from './guards';
import {
  createStudioApi,
  parseStudioCodeSent,
  parseStudioGallery,
  parseStudioGalleryPage,
  parseStudioLogin,
  parseStudioSession,
} from './studioApi';

describe('the parsers of the web area', () => {
  it('reads the user of a session', () => {
    expect(parseStudioSession({ user: studioUserPayload })).toEqual(studioUserPayload);
  });

  it('accepts a user without the masked email (the session endpoint does not send it)', () => {
    const withoutEmail = Object.fromEntries(Object.entries(studioUserPayload).filter(([key]) => key !== 'maskedEmail'));

    expect(parseStudioSession({ user: withoutEmail }).maskedEmail).toBe('');
  });

  it('points at what is wrong in a user', () => {
    expect(() => parseStudioSession({ user: { ...studioUserPayload, name: 5 } })).toThrow('session.user.name');
    expect(() => parseStudioSession({})).toThrow('session.user');
  });

  it('tells a complete sign-in from one that waits for its code', () => {
    expect(parseStudioLogin({ status: 'SignedIn', user: studioUserPayload })).toEqual({ status: 'SignedIn', user: studioUserPayload });
    expect(parseStudioLogin({ status: 'CodeRequired', maskedEmail: 'a***@example.com', expiresAt: '2026-10-30T12:10:00+00:00' })).toEqual({
      status: 'CodeRequired',
      maskedEmail: 'a***@example.com',
      expiresAt: '2026-10-30T12:10:00+00:00',
    });
  });

  it('refuses a status it does not know', () => {
    expect(() => parseStudioLogin({ status: 'Maybe' })).toThrow(ResponseShapeError);
    expect(() => parseStudioLogin({ status: 'CodeRequired', maskedEmail: 'a***@example.com' })).toThrow('login.expiresAt');
  });

  it('reads where a new code went', () => {
    expect(parseStudioCodeSent({ maskedEmail: 'a***@example.com', expiresAt: '2026-10-30T12:10:00+00:00' }).maskedEmail).toBe('a***@example.com');
    expect(() => parseStudioCodeSent({})).toThrow('codeSent.maskedEmail');
  });

  it('reads a gallery with its terms', () => {
    const gallery = parseStudioGallery(galleryPayload, 'gallery');

    expect(gallery.status).toBe('Pending');
    expect(gallery.selectablePhotos).toBe(35);
    expect(gallery.extraPhotoPrice).toEqual({ amount: 3000, currency: 'CRC' });
    expect(gallery.allowedActions).toEqual(['EditTerms']);
  });

  it('refuses a gallery with a state, an action or a count it does not know', () => {
    expect(() => parseStudioGallery({ ...galleryPayload, status: 'Lost' }, 'gallery')).toThrow('gallery.status');
    expect(() => parseStudioGallery({ ...galleryPayload, allowedActions: ['Explode'] }, 'gallery')).toThrow('allowedActions[0]');
    expect(() => parseStudioGallery({ ...galleryPayload, bonusPhotos: -1 }, 'gallery')).toThrow('bonusPhotos');
    expect(() => parseStudioGallery({ ...galleryPayload, includedPhotos: 1.5 }, 'gallery')).toThrow('includedPhotos');
  });

  it('reads a page and its cursor, and takes a missing cursor as the last page', () => {
    expect(parseStudioGalleryPage({ items: [galleryPayload], nextCursor: '639.abc' }).nextCursor).toBe('639.abc');
    expect(parseStudioGalleryPage({ items: [] }).nextCursor).toBeNull();
    expect(() => parseStudioGalleryPage([])).toThrow(ResponseShapeError);
  });
});

describe('the client of the web area', () => {
  /**
   * Builds the API over a client that sends cookies and a fetch that answers one thing.
   * @param response What the server answers.
   * @returns The API and the fetch mock.
   */
  function apiAnswering(response: Response): { readonly api: ReturnType<typeof createStudioApi>; readonly fetchMock: ReturnType<typeof vi.fn<typeof fetch>> } {
    // Every request gets its own copy: the body of an answer can be read only once.
    const fetchMock = vi.fn<typeof fetch>().mockImplementation(() => Promise.resolve(response.clone()));
    return { api: createStudioApi(createHttpClient('https://api.example.test', { fetchFn: fetchMock, withCredentials: true })), fetchMock };
  }

  it('sends the cookies of the API with every request', async () => {
    const { api, fetchMock } = apiAnswering(jsonResponse(200, { user: studioUserPayload }));

    await api.getSession();

    expect(fetchMock.mock.calls[0]?.[1]).toMatchObject({ credentials: 'include' });
  });

  it('does not send cookies unless it is asked to, as the client portal does', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(200, { x: 1 }));

    await createHttpClient('https://api.example.test', { fetchFn: fetchMock }).get('/x', (body) => body);

    expect(fetchMock.mock.calls[0]?.[1]).not.toHaveProperty('credentials');
  });

  it('sends the password, the code, the resend and the sign-out to their paths with their bodies', async () => {
    const { api, fetchMock } = apiAnswering(jsonResponse(200, { status: 'SignedIn', user: studioUserPayload }));

    await api.login('ana', 'a password');
    fetchMock.mockResolvedValue(jsonResponse(200, { user: studioUserPayload }));
    await api.verifyCode('123456', true);
    fetchMock.mockResolvedValue(jsonResponse(202, { maskedEmail: 'a***@example.com', expiresAt: '2026-10-30T12:10:00+00:00' }));
    await api.resendCode();

    const calls = fetchMock.mock.calls.map(([url, init]) => ({ url: String(url), method: init?.method, body: String(init?.body) }));
    expect(calls).toEqual([
      { url: 'https://api.example.test/api/web/auth/login', method: 'POST', body: '{"username":"ana","password":"a password"}' },
      { url: 'https://api.example.test/api/web/auth/code/verify', method: 'POST', body: '{"code":"123456","rememberDevice":true}' },
      { url: 'https://api.example.test/api/web/auth/code/resend', method: 'POST', body: '{}' },
    ]);
  });

  it('signs out with an answer that has no content', async () => {
    const { api, fetchMock } = apiAnswering(new Response(null, { status: 204 }));

    await expect(api.logout(true)).resolves.toBeUndefined();

    expect(String(fetchMock.mock.calls[0]?.[0])).toBe('https://api.example.test/api/web/auth/logout');
    expect(fetchMock.mock.calls[0]?.[1]?.body).toBe('{"forgetDevice":true}');
  });

  it('asks for the galleries a page at a time, with the cursor encoded', async () => {
    const { api, fetchMock } = apiAnswering(jsonResponse(200, { items: [], nextCursor: null }));

    await api.listGalleries(null);
    await api.listGalleries('639.0197a000/+x');

    expect(fetchMock.mock.calls.map(([url]) => String(url))).toEqual([
      'https://api.example.test/api/web/galleries?limit=5',
      'https://api.example.test/api/web/galleries?limit=5&cursor=639.0197a000%2F%2Bx',
    ]);
  });

  it('turns an answer that is not a success into an error with its stable code', async () => {
    const { api } = apiAnswering(jsonResponse(422, { code: 'auth.invalid_login_code', detail: 'No' }));

    const error = await api.verifyCode('000000', false).catch((failure: unknown) => failure);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).code).toBe('auth.invalid_login_code');
  });
});
