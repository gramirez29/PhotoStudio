import { fireEvent, render, screen } from '@testing-library/react-native';
import { useAppearanceStore } from '../../appearance/appearanceStore';
import { getPalette } from '../../theme/palettes';
import type { ColorScheme } from '../../types/theme/palette.types';
import { AppThemeProvider } from '../AppThemeProvider';
import { ToggleRow } from '../ToggleRow';

jest.mock('expo-secure-store', () => ({ getItemAsync: jest.fn(), setItemAsync: jest.fn(), deleteItemAsync: jest.fn() }));

/** The track and the thumb are drawings hidden from the screen reader, so a test has to ask for the hidden ones. */
const HIDDEN = { includeHiddenElements: true } as const;

/** Smallest contrast WCAG asks for in what is not text but has to be seen, such as the parts of a control. */
const NON_TEXT_CONTRAST = 3;

/**
 * Computes the relative luminance of a color, as WCAG defines it.
 * @param hex Color as #rrggbb.
 * @returns The luminance, from 0 (black) to 1 (white).
 */
function luminance(hex: string): number {
  const [red = 0, green = 0, blue = 0] = [1, 3, 5]
    .map((offset) => parseInt(hex.slice(offset, offset + 2), 16) / 255)
    .map((value) => (value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4));
  return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
}

/**
 * Computes the contrast ratio between two colors, as WCAG defines it.
 * @param first First color as #rrggbb.
 * @param second Second color as #rrggbb.
 * @returns The ratio, from 1 (none) to 21.
 */
function contrast(first: string, second: string): number {
  const [light = 0, dark = 0] = [luminance(first), luminance(second)].sort((a, b) => b - a);
  return (light + 0.05) / (dark + 0.05);
}

/**
 * Draws the row in a theme, the way the app does.
 * @param scheme Theme to draw it in.
 * @param value Whether the switch is on.
 * @param onValueChange Called when the row is tapped.
 */
function renderRow(scheme: ColorScheme, value: boolean, onValueChange = jest.fn()): void {
  useAppearanceStore.setState({ preference: scheme });
  render(
    <AppThemeProvider>
      <ToggleRow label="Mostrar todas" value={value} onValueChange={onValueChange} />
    </AppThemeProvider>,
  );
}

describe('ToggleRow', () => {
  it('says it is a switch and whether it is on, and takes the label', () => {
    renderRow('light', true);

    expect(screen.getByRole('switch', { name: 'Mostrar todas' }).props.accessibilityState).toMatchObject({ checked: true });
    expect(screen.getByText('Mostrar todas')).toBeTruthy();
  });

  it('asks for the opposite value when the row is tapped', () => {
    const onValueChange = jest.fn();
    renderRow('light', false, onValueChange);

    fireEvent.press(screen.getByRole('switch'));

    expect(onValueChange).toHaveBeenCalledWith(true);
  });

  it('asks to turn it off when it is on', () => {
    const onValueChange = jest.fn();
    renderRow('light', true, onValueChange);

    fireEvent.press(screen.getByRole('switch'));

    expect(onValueChange).toHaveBeenCalledWith(false);
  });

  it('uses a longer name for a screen reader when it is given', () => {
    render(<ToggleRow label="Todas" accessibilityLabel="Mostrar todas, también las canceladas" value={false} onValueChange={jest.fn()} />);

    expect(screen.getByRole('switch', { name: 'Mostrar todas, también las canceladas' })).toBeTruthy();
  });

  describe.each<ColorScheme>(['light', 'dark'])('in the %s theme', (scheme) => {
    const palette = getPalette(scheme);

    it('draws the off state with the soft track, its outline and a darker thumb, all from the theme', () => {
      renderRow(scheme, false);

      expect(screen.getByTestId('toggle-track', HIDDEN)).toHaveStyle({ backgroundColor: palette.surfaceVariant, borderColor: palette.outline });
      expect(screen.getByTestId('toggle-thumb', HIDDEN)).toHaveStyle({ backgroundColor: palette.outline, width: 16 });
    });

    it('draws the on state with the main color and a thumb on it', () => {
      renderRow(scheme, true);

      expect(screen.getByTestId('toggle-track', HIDDEN)).toHaveStyle({ backgroundColor: palette.primary, borderColor: palette.primary });
      expect(screen.getByTestId('toggle-thumb', HIDDEN)).toHaveStyle({ backgroundColor: palette.onPrimary, width: 24 });
    });

    it('keeps every part of the switch apart from what is behind it, in both states', () => {
      // The menu card is drawn on the surface color.
      expect(contrast(palette.outline, palette.surface)).toBeGreaterThanOrEqual(NON_TEXT_CONTRAST); // outline of the off track
      expect(contrast(palette.outline, palette.surfaceVariant)).toBeGreaterThanOrEqual(NON_TEXT_CONTRAST); // off thumb on its track
      expect(contrast(palette.primary, palette.surface)).toBeGreaterThanOrEqual(NON_TEXT_CONTRAST); // on track
      expect(contrast(palette.onPrimary, palette.primary)).toBeGreaterThanOrEqual(NON_TEXT_CONTRAST); // on thumb on its track
    });
  });
});
