import { FONT_ASSETS, FONT_FAMILIES } from '../fonts';
import { DARK_PALETTE, LIGHT_PALETTE } from '../palettes';
import { buildPaperTheme } from '../paperTheme';
import { colors } from '../tokens';

describe('buildPaperTheme', () => {
  it('paints the library with the light palette of the brand', () => {
    const theme = buildPaperTheme('light');

    expect(theme.dark).toBe(false);
    expect(theme.colors.primary).toBe(LIGHT_PALETTE.primary);
    expect(theme.colors.onPrimary).toBe(LIGHT_PALETTE.onPrimary);
    expect(theme.colors.background).toBe(LIGHT_PALETTE.background);
    expect(theme.colors.surfaceVariant).toBe(LIGHT_PALETTE.surfaceVariant);
    expect(theme.colors.outline).toBe(LIGHT_PALETTE.outline);
    expect(theme.colors.error).toBe(LIGHT_PALETTE.error);
  });

  it('paints the library with the dark palette of the brand', () => {
    const theme = buildPaperTheme('dark');

    expect(theme.dark).toBe(true);
    expect(theme.colors.primary).toBe(DARK_PALETTE.primary);
    expect(theme.colors.background).toBe(DARK_PALETTE.background);
    expect(theme.colors.onSurface).toBe(DARK_PALETTE.onSurface);
    expect(theme.colors.surfaceVariant).toBe(DARK_PALETTE.surfaceVariant);
  });

  it('keeps the roles the palette does not name, so no component of the library lacks a color', () => {
    const theme = buildPaperTheme('light');

    expect(theme.colors.elevation.level3).toBeDefined();
    expect(theme.colors.surfaceDisabled).toBeDefined();
    expect(theme.colors.inverseSurface).toBeDefined();
  });

  it('shades what is behind a dialog with a translucent color, never an opaque one', () => {
    expect(buildPaperTheme('light').colors.backdrop).toMatch(/^rgba\(.+, 0\.45\)$/);
    expect(buildPaperTheme('dark').colors.backdrop).toMatch(/^rgba\(.+, 0\.45\)$/);
  });

  it('does not take colors from the wallpaper of the phone: the theme is the same every time', () => {
    expect(buildPaperTheme('light').colors).toEqual(buildPaperTheme('light').colors);
  });

  it('writes titles in Josefin Sans and the rest in DM Sans, the fonts of the web', () => {
    const { fonts } = buildPaperTheme('light');

    expect(fonts.headlineSmall.fontFamily).toBe(FONT_FAMILIES.title);
    expect(fonts.titleLarge.fontFamily).toBe(FONT_FAMILIES.title);
    expect(fonts.bodyLarge.fontFamily).toBe(FONT_FAMILIES.body);
    expect(fonts.labelLarge.fontFamily).toBe(FONT_FAMILIES.bodyMedium);
    expect(fonts.default.fontFamily).toBe(FONT_FAMILIES.body);
  });

  it('asks only for fonts that the app loads', () => {
    const loaded = Object.keys(FONT_ASSETS);
    const asked = Object.values(buildPaperTheme('dark').fonts).map((font) => font.fontFamily);

    expect(asked.every((family) => loaded.includes(family))).toBe(true);
  });
});

describe('colors, the compatibility layer', () => {
  it('keeps the names the components use, with the colors of the light palette', () => {
    expect(colors.background).toBe(LIGHT_PALETTE.background);
    expect(colors.surface).toBe(LIGHT_PALETTE.surfaceVariant);
    expect(colors.textPrimary).toBe(LIGHT_PALETTE.onBackground);
    expect(colors.primary).toBe(LIGHT_PALETTE.primary);
    expect(colors.danger).toBe(LIGHT_PALETTE.error);
    expect(Object.keys(colors)).toEqual([
      'background',
      'surface',
      'border',
      'textPrimary',
      'textSecondary',
      'textInverse',
      'primary',
      'success',
      'warning',
      'danger',
      'info',
      'muted',
      'overlay',
    ]);
  });
});
