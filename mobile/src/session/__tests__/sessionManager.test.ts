import type { AuthSessionResponse } from '../../types/api/auth';

jest.mock('../../api/authClient', () => ({
  authApi: { login: jest.fn(), refresh: jest.fn(), logout: jest.fn() },
}));
jest.mock('../../storage/refreshTokenStorage', () => ({
  saveRefreshToken: jest.fn(() => Promise.resolve()),
  loadRefreshToken: jest.fn(() => Promise.resolve(null)),
  clearRefreshToken: jest.fn(() => Promise.resolve()),
}));

/**
 * Loads a fresh copy of the session manager and everything it shares state with. The manager keeps module-level state (the
 * refresh token in memory and the renewal in progress), so each test starts from a clean registry.
 * @returns The manager, the store, the mocked ports and the error class of the same registry.
 */
function load() {
  jest.resetModules();
  const manager = jest.requireActual<typeof import('../sessionManager')>('../sessionManager');
  const { useSessionStore } = jest.requireActual<typeof import('../sessionStore')>('../sessionStore');
  const { authApi } = jest.requireMock<typeof import('../../api/authClient')>('../../api/authClient');
  const storage = jest.requireMock<typeof import('../../storage/refreshTokenStorage')>('../../storage/refreshTokenStorage');
  const { ApiError } = jest.requireActual<typeof import('../../api/httpClient')>('../../api/httpClient');

  return {
    manager,
    store: useSessionStore,
    login: jest.mocked(authApi.login),
    refresh: jest.mocked(authApi.refresh),
    logout: jest.mocked(authApi.logout),
    saveRefreshToken: jest.mocked(storage.saveRefreshToken),
    loadRefreshToken: jest.mocked(storage.loadRefreshToken),
    clearRefreshToken: jest.mocked(storage.clearRefreshToken),
    ApiError,
  };
}

/**
 * Builds the session the backend would return.
 * @param overrides Values to replace.
 * @returns The session.
 */
function backendSession(overrides: Partial<AuthSessionResponse> = {}): AuthSessionResponse {
  return {
    accessToken: 'access-1',
    accessTokenExpiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
    refreshToken: 'refresh-1',
    refreshTokenExpiresAt: new Date(Date.now() + 30 * 24 * 3_600_000).toISOString(),
    photographerId: '0199a1b2-0000-7000-8000-000000000002',
    email: 'ana@example.com',
    ...overrides,
  };
}

describe('signIn', () => {
  it('keeps the session in the store and the refresh token in secure storage', async () => {
    const t = load();
    t.login.mockResolvedValue(backendSession());

    await t.manager.signIn('ana@example.com', 'secret password');

    expect(t.login).toHaveBeenCalledWith('ana@example.com', 'secret password');
    expect(t.saveRefreshToken).toHaveBeenCalledWith('refresh-1');
    const state = t.store.getState();
    expect(state.status).toBe('signedIn');
    expect(state.session).toMatchObject({
      accessToken: 'access-1',
      photographerId: '0199a1b2-0000-7000-8000-000000000002',
      email: 'ana@example.com',
    });
    expect(state.session?.accessTokenExpiresAt).toBeGreaterThan(Date.now());
  });

  it('leaves the app signed out and saves nothing when the credentials are refused', async () => {
    const t = load();
    t.store.getState().setSignedOut();
    t.login.mockRejectedValue(new t.ApiError(401, 'x', 'auth.invalid_credentials'));

    await expect(t.manager.signIn('ana@example.com', 'wrong')).rejects.toMatchObject({ code: 'auth.invalid_credentials' });

    expect(t.store.getState().status).toBe('signedOut');
    expect(t.saveRefreshToken).not.toHaveBeenCalled();
  });
});

describe('restoreSession', () => {
  it('signs out when no session was saved', async () => {
    const t = load();
    t.loadRefreshToken.mockResolvedValue(null);

    await t.manager.restoreSession();

    expect(t.store.getState().status).toBe('signedOut');
    expect(t.refresh).not.toHaveBeenCalled();
  });

  it('renews a saved session and replaces the saved refresh token', async () => {
    const t = load();
    t.loadRefreshToken.mockResolvedValue('saved-refresh');
    t.refresh.mockResolvedValue(backendSession({ accessToken: 'access-2', refreshToken: 'refresh-2' }));

    await t.manager.restoreSession();

    expect(t.refresh).toHaveBeenCalledWith('saved-refresh');
    expect(t.saveRefreshToken).toHaveBeenCalledWith('refresh-2');
    expect(t.store.getState().status).toBe('signedIn');
    expect(t.store.getState().session?.accessToken).toBe('access-2');
  });

  it('forgets a saved session the backend no longer accepts', async () => {
    const t = load();
    t.loadRefreshToken.mockResolvedValue('revoked-refresh');
    t.refresh.mockRejectedValue(new t.ApiError(401, 'x', 'auth.invalid_refresh_token'));

    await t.manager.restoreSession();

    expect(t.store.getState().status).toBe('signedOut');
    expect(t.clearRefreshToken).toHaveBeenCalled();
  });

  it('does not sign out for a network problem: it waits offline and keeps the saved session', async () => {
    const t = load();
    t.loadRefreshToken.mockResolvedValue('saved-refresh');
    t.refresh.mockRejectedValue(new TypeError('Network request failed'));

    await t.manager.restoreSession();

    expect(t.store.getState().status).toBe('offline');
    expect(t.clearRefreshToken).not.toHaveBeenCalled();
  });

  it('can be retried after being offline', async () => {
    const t = load();
    t.loadRefreshToken.mockResolvedValue('saved-refresh');
    t.refresh.mockRejectedValueOnce(new TypeError('Network request failed'));
    await t.manager.restoreSession();
    t.refresh.mockResolvedValue(backendSession({ refreshToken: 'refresh-2' }));

    await t.manager.restoreSession();

    expect(t.store.getState().status).toBe('signedIn');
  });
});

describe('getValidAccessToken', () => {
  it('returns null when there is no session', async () => {
    const t = load();

    await expect(t.manager.getValidAccessToken()).resolves.toBeNull();
  });

  it('returns the current token without renewing while it is not about to expire', async () => {
    const t = load();
    t.login.mockResolvedValue(backendSession());
    await t.manager.signIn('ana@example.com', 'secret password');

    await expect(t.manager.getValidAccessToken()).resolves.toBe('access-1');

    expect(t.refresh).not.toHaveBeenCalled();
  });

  it('renews the session first when the token is about to expire', async () => {
    const t = load();
    t.login.mockResolvedValue(backendSession({ accessTokenExpiresAt: new Date(Date.now() + 10_000).toISOString() }));
    await t.manager.signIn('ana@example.com', 'secret password');
    t.refresh.mockResolvedValue(backendSession({ accessToken: 'access-2', refreshToken: 'refresh-2' }));

    await expect(t.manager.getValidAccessToken()).resolves.toBe('access-2');

    expect(t.refresh).toHaveBeenCalledWith('refresh-1');
    expect(t.saveRefreshToken).toHaveBeenLastCalledWith('refresh-2');
  });

  it('renews only once when several requests need a new token at the same time', async () => {
    const t = load();
    t.login.mockResolvedValue(backendSession({ accessTokenExpiresAt: new Date(Date.now() + 10_000).toISOString() }));
    await t.manager.signIn('ana@example.com', 'secret password');
    let finish: (session: AuthSessionResponse) => void = () => undefined;
    t.refresh.mockReturnValue(new Promise<AuthSessionResponse>((resolve) => { finish = resolve; }));

    const requests = [t.manager.getValidAccessToken(), t.manager.getValidAccessToken(), t.manager.getValidAccessToken()];
    finish(backendSession({ accessToken: 'access-2', refreshToken: 'refresh-2' }));
    const tokens = await Promise.all(requests);

    expect(tokens).toEqual(['access-2', 'access-2', 'access-2']);
    expect(t.refresh).toHaveBeenCalledTimes(1);
  });

  it('ends the session when the backend rejects the refresh token while renewing', async () => {
    const t = load();
    t.login.mockResolvedValue(backendSession({ accessTokenExpiresAt: new Date(Date.now() + 10_000).toISOString() }));
    await t.manager.signIn('ana@example.com', 'secret password');
    t.refresh.mockRejectedValue(new t.ApiError(401, 'x', 'auth.invalid_refresh_token'));

    await expect(t.manager.getValidAccessToken()).resolves.toBeNull();

    expect(t.store.getState().status).toBe('signedOut');
    expect(t.clearRefreshToken).toHaveBeenCalled();
  });

  it('keeps the session and rethrows when renewing fails for a network problem', async () => {
    const t = load();
    t.login.mockResolvedValue(backendSession({ accessTokenExpiresAt: new Date(Date.now() + 10_000).toISOString() }));
    await t.manager.signIn('ana@example.com', 'secret password');
    t.refresh.mockRejectedValue(new TypeError('Network request failed'));

    await expect(t.manager.getValidAccessToken()).rejects.toThrow('Network request failed');

    expect(t.store.getState().status).toBe('signedIn');
    expect(t.clearRefreshToken).not.toHaveBeenCalled();
  });
});

describe('renewAfterRejection', () => {
  it('uses the token another request already obtained instead of renewing again', async () => {
    const t = load();
    t.login.mockResolvedValue(backendSession({ accessToken: 'access-newer' }));
    await t.manager.signIn('ana@example.com', 'secret password');

    await expect(t.manager.renewAfterRejection('access-older')).resolves.toBe('access-newer');

    expect(t.refresh).not.toHaveBeenCalled();
  });

  it('renews the session when the rejected token is the current one', async () => {
    const t = load();
    t.login.mockResolvedValue(backendSession());
    await t.manager.signIn('ana@example.com', 'secret password');
    t.refresh.mockResolvedValue(backendSession({ accessToken: 'access-2', refreshToken: 'refresh-2' }));

    await expect(t.manager.renewAfterRejection('access-1')).resolves.toBe('access-2');

    expect(t.refresh).toHaveBeenCalledWith('refresh-1');
  });
});

describe('signOut', () => {
  it('forgets the session at once and tells the backend to revoke it', async () => {
    const t = load();
    t.login.mockResolvedValue(backendSession());
    await t.manager.signIn('ana@example.com', 'secret password');
    t.logout.mockResolvedValue(undefined);

    await t.manager.signOut();

    expect(t.store.getState().status).toBe('signedOut');
    expect(t.store.getState().session).toBeNull();
    expect(t.clearRefreshToken).toHaveBeenCalled();
    expect(t.logout).toHaveBeenCalledWith('refresh-1');
  });

  it('signs out even when the backend cannot be reached', async () => {
    const t = load();
    t.login.mockResolvedValue(backendSession());
    await t.manager.signIn('ana@example.com', 'secret password');
    t.logout.mockRejectedValue(new TypeError('Network request failed'));

    await expect(t.manager.signOut()).resolves.toBeUndefined();

    expect(t.store.getState().status).toBe('signedOut');
  });

  it('does not call the backend when there was no refresh token', async () => {
    const t = load();
    t.loadRefreshToken.mockResolvedValue(null);

    await t.manager.signOut();

    expect(t.logout).not.toHaveBeenCalled();
    expect(t.store.getState().status).toBe('signedOut');
  });
});
