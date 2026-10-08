import { DEFAULT_API_URL, normalizeApiUrl } from '../envNormalizers';

describe('normalizeApiUrl', () => {
  it('falls back to the local API when the variable is missing or blank', () => {
    expect(normalizeApiUrl(undefined)).toBe(DEFAULT_API_URL);
    expect(normalizeApiUrl('   ')).toBe(DEFAULT_API_URL);
  });

  it('removes trailing slashes', () => {
    expect(normalizeApiUrl('https://api.photostud.app///')).toBe('https://api.photostud.app');
  });
});
