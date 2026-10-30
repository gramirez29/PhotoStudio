import { useState, type ReactElement } from 'react';
import { Image, StyleSheet, useWindowDimensions, View, type LayoutChangeEvent } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import { WATERMARK_OPACITY, WATERMARK_SCALE } from '../theme/background';
import type { AppBackgroundProps } from '../types/components/AppBackground.types';

/** The logo as a white shape on a transparent background (see `scripts/make-watermark.ps1`); the theme gives it its color. */
const WATERMARK_IMAGE = require('../../assets/watermark.png') as number;

/**
 * Paints what is behind a whole screen: the flat color of the theme with the logo of the studio, large and faint, in the middle.
 * The color is opaque, which matters while screens slide over each other.
 *
 * The logo is sized from the width of the window and centered in the space the screen really has: below the header, not in the
 * whole window, or it would look low. Until the space is measured, the height of the window stands in.
 * It is decoration: hidden from the screen reader.
 * @param props Component props.
 * @returns The background with the screen on top.
 */
export function AppBackground({ children }: AppBackgroundProps): ReactElement {
  const { palette, scheme } = useAppTheme();
  const { width, height } = useWindowDimensions();
  const [measuredHeight, setMeasuredHeight] = useState<number | null>(null);
  const areaHeight = measuredHeight ?? height;
  const size = Math.min(width, areaHeight) * WATERMARK_SCALE;

  /** Keeps the height this background really has, to center the logo in it. */
  const measure = (event: LayoutChangeEvent): void => setMeasuredHeight(event.nativeEvent.layout.height);

  return (
    <View style={[styles.root, { backgroundColor: palette.background }]} onLayout={measure}>
      <Image
        source={WATERMARK_IMAGE}
        resizeMode="contain"
        accessible={false}
        importantForAccessibility="no"
        style={[
          styles.layer,
          {
            width: size,
            height: size,
            left: (width - size) / 2,
            top: (areaHeight - size) / 2,
            tintColor: palette.primary,
            opacity: WATERMARK_OPACITY[scheme],
          },
        ]}
      />
      {children}
    </View>
  );
}

/** Styles of the background. */
const styles = StyleSheet.create({
  root: {
    flex: 1,
  },
  layer: {
    position: 'absolute',
  },
});
