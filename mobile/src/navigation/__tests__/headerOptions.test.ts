import { FONT_FAMILIES } from '../../theme/fonts';
import { DARK_PALETTE, LIGHT_PALETTE } from '../../theme/palettes';
import { buildHeaderOptions } from '../headerOptions';

describe('buildHeaderOptions', () => {
  it('paints the header with the slate blue of the logo and writes in white, as the web does', () => {
    const options = buildHeaderOptions(LIGHT_PALETTE);

    expect(options.headerStyle.backgroundColor).toBe('#3a4466');
    expect(options.headerTintColor).toBe(LIGHT_PALETTE.onBrand);
    expect(options.headerTitleStyle.color).toBe(LIGHT_PALETTE.onBrand);
  });

  it('is the same band in the dark theme, because the brand color is the same in both', () => {
    expect(buildHeaderOptions(DARK_PALETTE).headerStyle).toEqual(buildHeaderOptions(LIGHT_PALETTE).headerStyle);
  });

  it('writes the title in Josefin Sans, centered, with no line under the band', () => {
    const options = buildHeaderOptions(LIGHT_PALETTE);

    expect(options.headerTitleStyle.fontFamily).toBe(FONT_FAMILIES.title);
    expect(options.headerTitleAlign).toBe('center');
    expect(options.headerShadowVisible).toBe(false);
  });
});
