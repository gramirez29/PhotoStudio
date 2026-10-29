import { act, renderHook } from '@testing-library/react-native';
import { useAppearanceStore } from '../../appearance/appearanceStore';
import { DARK_PALETTE, LIGHT_PALETTE } from '../palettes';
import { makeStyles } from '../makeStyles';

/** Styles written as a function of the palette, the way a component writes them. */
const useStyles = makeStyles((palette, scheme) => ({
  title: { color: palette.onBackground, fontSize: scheme === 'dark' ? 20 : 18 },
  card: { backgroundColor: palette.surfaceVariant, padding: 16 },
}));

describe('makeStyles', () => {
  beforeEach(() => {
    useAppearanceStore.setState({ preference: 'light' });
  });

  it('writes the styles with the colors of the light theme', () => {
    const { result } = renderHook(() => useStyles());

    expect(result.current.title).toMatchObject({ color: LIGHT_PALETTE.onBackground, fontSize: 18 });
    expect(result.current.card).toMatchObject({ backgroundColor: LIGHT_PALETTE.surfaceVariant, padding: 16 });
  });

  it('writes them again with the colors of the dark theme when the photographer chooses it', () => {
    const { result } = renderHook(() => useStyles());

    act(() => useAppearanceStore.getState().setPreference('dark'));

    expect(result.current.title).toMatchObject({ color: DARK_PALETTE.onBackground, fontSize: 20 });
    expect(result.current.card).toMatchObject({ backgroundColor: DARK_PALETTE.surfaceVariant });
  });

  it('keeps the same styles while the theme does not change, so a screen does not repaint for nothing', () => {
    const { result, rerender } = renderHook(() => useStyles());
    const first = result.current;

    rerender({});

    expect(result.current).toBe(first);
  });

  it('gives every component its own hook with its own styles', () => {
    const useOther = makeStyles((palette) => ({ label: { color: palette.primary } }));

    const { result } = renderHook(() => ({ styles: useStyles(), other: useOther() }));

    expect(result.current.other.label).toMatchObject({ color: LIGHT_PALETTE.primary });
    expect(result.current.styles.title).toBeDefined();
  });
});
