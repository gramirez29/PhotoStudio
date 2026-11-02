import { describe, expect, it } from 'vitest';
import { forgetSession, readStoredSession, storeSession } from './portalSession';

const NOW = Date.parse('2026-11-04T12:00:00Z');
const FRESH = { accessToken: 'jwt', expiresAt: new Date(NOW + 20 * 24 * 60 * 60_000).toISOString() };

/**
 * Builds a fake of the storage of the browser.
 * @returns The storage and what it holds.
 */
function fakeStorage(): Pick<Storage, 'getItem' | 'setItem' | 'removeItem'> & { readonly items: Map<string, string> } {
  const items = new Map<string, string>();
  return {
    items,
    getItem: (key) => items.get(key) ?? null,
    setItem: (key, value) => void items.set(key, value),
    removeItem: (key) => void items.delete(key),
  };
}

describe('the session remembered in the browser', () => {
  it('is stored for a link and read back, so the next visit does not ask for another code', () => {
    const storage = fakeStorage();
    storeSession('token-a', FRESH, storage);

    expect(readStoredSession('token-a', NOW, storage)).toEqual(FRESH);
    expect(readStoredSession('token-b', NOW, storage)).toBeNull();
  });

  it('is not used once it is about to expire, and it is dropped from the storage', () => {
    const storage = fakeStorage();
    storeSession('token-a', { accessToken: 'jwt', expiresAt: new Date(NOW + 30_000).toISOString() }, storage);

    expect(readStoredSession('token-a', NOW, storage)).toBeNull();
    expect(storage.items.size).toBe(0);
  });

  it('is forgotten when the client closes it', () => {
    const storage = fakeStorage();
    storeSession('token-a', FRESH, storage);
    forgetSession('token-a', storage);

    expect(readStoredSession('token-a', NOW, storage)).toBeNull();
  });

  it('reads nothing from text that is not what the page wrote, and drops it', () => {
    const storage = fakeStorage();
    for (const text of ['not json', '{"accessToken":3,"expiresAt":"x"}', '{"accessToken":"jwt"}', '[]', 'null']) {
      storage.items.set('photosense.portal.session.token-a', text);
      expect(readStoredSession('token-a', NOW, storage)).toBeNull();
      expect(storage.items.size).toBe(0);
    }
  });

  it('survives a storage that is blocked', () => {
    const blocked = {
      getItem: (): string | null => {
        throw new Error('blocked');
      },
      setItem: (): void => {
        throw new Error('blocked');
      },
      removeItem: (): void => {
        throw new Error('blocked');
      },
    };

    expect(readStoredSession('token-a', NOW, blocked)).toBeNull();
    expect(() => storeSession('token-a', FRESH, blocked)).not.toThrow();
    expect(() => forgetSession('token-a', blocked)).not.toThrow();
  });
});
