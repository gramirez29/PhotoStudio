import { fireEvent, render, screen } from '@testing-library/react-native';
import { Image, StyleSheet, Text, useWindowDimensions, View } from 'react-native';
import { useAppearanceStore } from '../../appearance/appearanceStore';
import { appScreenLayout } from '../../navigation/appScreenLayout';
import { WATERMARK_OPACITY, WATERMARK_SCALE } from '../../theme/background';
import { DARK_PALETTE, LIGHT_PALETTE } from '../../theme/palettes';
import { AppBackground } from '../AppBackground';

describe('AppBackground', () => {
  beforeEach(() => {
    useAppearanceStore.setState({ preference: 'light' });
  });

  it('shows the screen on top of the logo', () => {
    render(
      <AppBackground>
        <Text>contenido</Text>
      </AppBackground>,
    );

    expect(screen.getByText('contenido')).toBeTruthy();
    expect(screen.UNSAFE_getAllByType(Image)).toHaveLength(1);
  });

  it('tints the logo with the main color of the light theme, faintly', () => {
    render(<AppBackground />);

    expect(StyleSheet.flatten(screen.UNSAFE_getByType(Image).props.style)).toMatchObject({
      tintColor: LIGHT_PALETTE.primary,
      opacity: WATERMARK_OPACITY.light,
    });
  });

  it('tints it with the main color of the dark theme', () => {
    useAppearanceStore.setState({ preference: 'dark' });

    render(<AppBackground />);

    expect(StyleSheet.flatten(screen.UNSAFE_getByType(Image).props.style)).toMatchObject({
      tintColor: DARK_PALETTE.primary,
      opacity: WATERMARK_OPACITY.dark,
    });
  });

  it('is opaque under the logo, so a screen never shows the one below it while sliding', () => {
    const { toJSON } = render(<AppBackground />);

    const root = toJSON();
    const rootStyle = StyleSheet.flatten(!Array.isArray(root) && root !== null ? root.props.style : undefined);
    expect(rootStyle).toMatchObject({ backgroundColor: LIGHT_PALETTE.background, flex: 1 });
  });

  it('sizes and centers the logo from the window, not from its container', () => {
    let window = { width: 0, height: 0 };
    function Probe(): null {
      window = useWindowDimensions();
      return null;
    }
    render(
      <>
        <Probe />
        <AppBackground />
      </>,
    );

    const size = Math.min(window.width, window.height) * WATERMARK_SCALE;
    expect(StyleSheet.flatten(screen.UNSAFE_getByType(Image).props.style)).toMatchObject({
      position: 'absolute',
      width: size,
      height: size,
      left: (window.width - size) / 2,
      top: (window.height - size) / 2,
    });
    expect(window.height).toBeGreaterThan(0);
  });

  it('centers the logo in the space below the header once it is measured, which is higher than the window', () => {
    const { toJSON } = render(<AppBackground />);
    const root = toJSON();
    expect(root).not.toBeNull();

    fireEvent(screen.UNSAFE_getAllByType(View)[0]!, 'layout', { nativeEvent: { layout: { x: 0, y: 0, width: 400, height: 600 } } });

    const logo = StyleSheet.flatten(screen.UNSAFE_getByType(Image).props.style);
    expect(logo.top).toBe((600 - logo.height) / 2);
  });

  it('is decoration: hidden from the screen reader', () => {
    render(<AppBackground />);

    const logo = screen.UNSAFE_getByType(Image);
    expect(logo.props.accessible).toBe(false);
    expect(logo.props.importantForAccessibility).toBe('no');
  });

  it('is what the stack puts under each of its screens', () => {
    render(appScreenLayout({ children: <Text>una pantalla</Text> }));

    expect(screen.getByText('una pantalla')).toBeTruthy();
    expect(screen.UNSAFE_getAllByType(Image)).toHaveLength(1);
  });
});
