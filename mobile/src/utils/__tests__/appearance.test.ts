import { isThemePreference, resolveColorScheme, statusBarStyleFor, themePreferenceLabel } from '../appearance';

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

describe('statusBarStyleFor', () => {
  it('uses light icons over the band of the brand, in both themes, because the band is dark in both', () => {
    expect(statusBarStyleFor(true, 'light')).toBe('light');
    expect(statusBarStyleFor(true, 'dark')).toBe('light');
  });

  it('follows the theme where the screen itself is under the status bar', () => {
    expect(statusBarStyleFor(false, 'light')).toBe('dark');
    expect(statusBarStyleFor(false, 'dark')).toBe('light');
  });
});
