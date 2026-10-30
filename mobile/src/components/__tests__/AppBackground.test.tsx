import { render, screen } from '@testing-library/react-native';
import { Image, StyleSheet, Text, useWindowDimensions, View } from 'react-native';
import { useAppearanceStore } from '../../appearance/appearanceStore';
import { appScreenLayout } from '../../navigation/appScreenLayout';
import { WASH_OPACITY } from '../../theme/background';
import { DARK_PALETTE, LIGHT_PALETTE } from '../../theme/palettes';
import { AppBackground } from '../AppBackground';

describe('AppBackground', () => {
  beforeEach(() => {
    useAppearanceStore.setState({ preference: 'light' });
  });

  it('shows the screen on top of the picture', () => {
    render(
      <AppBackground>
        <Text>contenido</Text>
      </AppBackground>,
    );

    expect(screen.getByText('contenido')).toBeTruthy();
    expect(screen.UNSAFE_getAllByType(Image)).toHaveLength(1);
  });

  it('washes the picture in white in the light theme, so the text reads over it', () => {
    render(<AppBackground />);

    const views = screen.UNSAFE_getAllByType(View);
    const wash = views.find((view) => StyleSheet.flatten(view.props.style)?.opacity === WASH_OPACITY.light);
    expect(StyleSheet.flatten(wash?.props.style)).toMatchObject({ backgroundColor: LIGHT_PALETTE.wash });
    expect(LIGHT_PALETTE.wash).toBe('#ffffff');
  });

  it('washes it in navy in the dark theme', () => {
    useAppearanceStore.setState({ preference: 'dark' });

    render(<AppBackground />);

    const views = screen.UNSAFE_getAllByType(View);
    const wash = views.find((view) => StyleSheet.flatten(view.props.style)?.opacity === WASH_OPACITY.dark);
    expect(StyleSheet.flatten(wash?.props.style)).toMatchObject({ backgroundColor: DARK_PALETTE.wash });
  });

  it('is opaque under the picture, so a screen never shows the one below it while sliding', () => {
    const { toJSON } = render(<AppBackground />);

    const root = toJSON();
    const rootStyle = StyleSheet.flatten(!Array.isArray(root) && root !== null ? root.props.style : undefined);
    expect(rootStyle).toMatchObject({ backgroundColor: LIGHT_PALETTE.background, flex: 1 });
  });

  it('makes the picture and the wash as big as the window, so they reach the bottom even if their container does not', () => {
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

    const picture = StyleSheet.flatten(screen.UNSAFE_getByType(Image).props.style);
    expect(picture).toMatchObject({ width: window.width, height: window.height, top: 0, left: 0, position: 'absolute' });
    const wash = screen.UNSAFE_getAllByType(View).find((view) => view.props.pointerEvents === 'none');
    expect(StyleSheet.flatten(wash?.props.style)).toMatchObject({ width: window.width, height: window.height });
    expect(window.height).toBeGreaterThan(0);
  });

  it('is decoration: hidden from the screen reader and never takes a touch', () => {
    render(<AppBackground />);

    expect(screen.UNSAFE_getByType(Image).props.accessible).toBe(false);
    const wash = screen.UNSAFE_getAllByType(View).find((view) => view.props.pointerEvents === 'none');
    expect(wash?.props.importantForAccessibility).toBe('no-hide-descendants');
  });

  it('is what the stack puts under each of its screens', () => {
    render(appScreenLayout({ children: <Text>una pantalla</Text> }));

    expect(screen.getByText('una pantalla')).toBeTruthy();
    expect(screen.UNSAFE_getAllByType(Image)).toHaveLength(1);
  });
});
