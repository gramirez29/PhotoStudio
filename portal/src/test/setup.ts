import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

/**
 * Makes sure the tests have a working local storage. Some Node versions expose an experimental `localStorage` that has no methods
 * unless a file is given, and it hides the one of jsdom; in that case a small in-memory storage takes its place.
 */
function ensureLocalStorage(): void {
  if (typeof window.localStorage?.clear === 'function') {
    return;
  }

  const items = new Map<string, string>();
  const storage: Storage = {
    get length(): number {
      return items.size;
    },
    clear: () => items.clear(),
    getItem: (key) => items.get(key) ?? null,
    key: (index) => [...items.keys()][index] ?? null,
    removeItem: (key) => void items.delete(key),
    setItem: (key, value) => void items.set(key, String(value)),
  };
  Object.defineProperty(window, 'localStorage', { value: storage, configurable: true });
}

ensureLocalStorage();

afterEach(() => {
  cleanup();
  // The session of a link is remembered in the browser: a test must not inherit the one of the previous test.
  window.localStorage.clear();
});
