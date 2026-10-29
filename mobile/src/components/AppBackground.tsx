import type { ReactElement } from 'react';
import { Image, StyleSheet, View } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import { WASH_OPACITY } from '../theme/background';
import type { AppBackgroundProps } from '../types/components/AppBackground.types';

/** The picture behind the app, already blurred (see `scripts/blur-background.ps1`). */
const BACKGROUND_IMAGE = require('../../assets/background.jpg') as number;

/**
 * Paints what is behind a whole screen: the blurred picture of the camera gear with a wash over it, white in the light theme and
 * navy in the dark one, so it reads as a soft texture. The base is opaque, which matters while screens slide over each other.
 * The picture and the wash are decoration: they are hidden from the screen reader and never take a touch.
 * @param props Component props.
 * @returns The background with the screen on top.
 */
export function AppBackground({ children }: AppBackgroundProps): ReactElement {
  const { palette, scheme } = useAppTheme();

  return (
    <View style={[styles.root, { backgroundColor: palette.background }]}>
      <Image
        source={BACKGROUND_IMAGE}
        resizeMode="cover"
        accessible={false}
        importantForAccessibility="no"
        style={StyleSheet.absoluteFill}
      />
      <View
        pointerEvents="none"
        importantForAccessibility="no-hide-descendants"
        style={[StyleSheet.absoluteFill, { backgroundColor: palette.wash, opacity: WASH_OPACITY[scheme] }]}
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
});
