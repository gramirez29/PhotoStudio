import { isThemePreference, resolveColorScheme, themePreferenceLabel } from '../appearance';

describe('isThemePreference', () => {
  it.each(['system', 'light', 'dark'])('accepts %s', (value) => {
    expect(isThemePreference(value)).toBe(true);
  });

  it.each(['auto', '', 'DARK', null, undefined, 3])('rejects %s', (value) => {
    expect(isThemePreference(value)).toBe(false);
  });
});

describe('resolveColorScheme', () => {
  it('shows what the photographer chose, whatever the phone says', () => {
    expect(resolveColorScheme('light', 'dark')).toBe('light');
    expect(resolveColorScheme('dark', 'light')).toBe('dark');
  });

  it('follows the phone when the photographer chose to', () => {
    expect(resolveColorScheme('system', 'dark')).toBe('dark');
    expect(resolveColorScheme('system', 'light')).toBe('light');
  });

  it('counts a phone that says nothing as light', () => {
    expect(resolveColorScheme('system', null)).toBe('light');
    expect(resolveColorScheme('system', undefined)).toBe('light');
    expect(resolveColorScheme('system', 'unspecified')).toBe('light');
  });
});

describe('themePreferenceLabel', () => {
  it('names each choice in Spanish', () => {
    expect(themePreferenceLabel('system')).toBe('Sistema');
    expect(themePreferenceLabel('light')).toBe('Claro');
    expect(themePreferenceLabel('dark')).toBe('Oscuro');
  });
});
