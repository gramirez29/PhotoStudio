import { act, renderHook } from '@testing-library/react-native';
import { useColorScheme } from 'react-native';
import { useAppearanceStore } from '../../appearance/appearanceStore';
import { DARK_PALETTE, LIGHT_PALETTE } from '../../theme/palettes';
import { useAppTheme } from '../useAppTheme';

jest.mock('react-native/Libraries/Utilities/useColorScheme', () => ({ default: jest.fn(), __esModule: true }));

describe('useAppTheme', () => {
  const systemIs = (scheme: 'light' | 'dark'): void => {
    jest.mocked(useColorScheme).mockReturnValue(scheme);
  };

  beforeEach(() => {
    useAppearanceStore.setState({ preference: 'light' });
    systemIs('dark');
  });

  it('shows light until the photographer chooses otherwise, even on a phone in dark', () => {
    const { result } = renderHook(() => useAppTheme());

    expect(result.current.scheme).toBe('light');
    expect(result.current.palette).toBe(LIGHT_PALETTE);
    expect(result.current.paperTheme.dark).toBe(false);
  });

  it('shows dark when the photographer chose it', () => {
    useAppearanceStore.setState({ preference: 'dark' });

    const { result } = renderHook(() => useAppTheme());

    expect(result.current.scheme).toBe('dark');
    expect(result.current.palette).toBe(DARK_PALETTE);
    expect(result.current.paperTheme.dark).toBe(true);
  });

  it('follows the phone when the photographer chose to', () => {
    useAppearanceStore.setState({ preference: 'system' });
    systemIs('dark');
    const dark = renderHook(() => useAppTheme());
    systemIs('light');
    const light = renderHook(() => useAppTheme());

    expect(dark.result.current.scheme).toBe('dark');
    expect(light.result.current.scheme).toBe('light');
  });

  it('changes as soon as the choice does', () => {
    const { result } = renderHook(() => useAppTheme());

    act(() => useAppearanceStore.getState().setPreference('dark'));

    expect(result.current.scheme).toBe('dark');
  });

  it('gives the same theme object while nothing changes, so the screens do not repaint for nothing', () => {
    const { result, rerender } = renderHook(() => useAppTheme());
    const first = result.current;

    rerender({});

    expect(result.current).toBe(first);
  });
});
