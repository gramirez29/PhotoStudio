import { DEFAULT_API_URL, normalizeApiUrl, normalizePhotographerId } from '../env';

describe('normalizeApiUrl', () => {
  it('falls back to the local API when the variable is missing or blank', () => {
    expect(normalizeApiUrl(undefined)).toBe(DEFAULT_API_URL);
    expect(normalizeApiUrl('   ')).toBe(DEFAULT_API_URL);
  });

  it('removes trailing slashes', () => {
    expect(normalizeApiUrl('https://api.photostud.app///')).toBe('https://api.photostud.app');
  });
});

describe('normalizePhotographerId', () => {
  it('returns null when the variable is missing or blank', () => {
    expect(normalizePhotographerId(undefined)).toBeNull();
    expect(normalizePhotographerId('   ')).toBeNull();
  });

  it('trims the identifier', () => {
    expect(normalizePhotographerId('  0197a000-0000-7000-8000-000000000001 ')).toBe('0197a000-0000-7000-8000-000000000001');
  });
});
