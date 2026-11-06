import { View, type ViewProps } from 'react-native';
import type { ReactElement } from 'react';

/** Stand-in for the canvas of Skia, which cannot draw in Jest: an empty view that keeps its style. */
export function Canvas(props: ViewProps): ReactElement {
  return <View {...props} />;
}

/** Stand-in for a path element of Skia: it draws nothing. */
export function Path(): null {
  return null;
}

/** Stand-in for the factory of Skia, with the few calls the charts make; the paths it builds ignore what is added to them. */
export const Skia = {
  Path: {
    Make: (): { addArc: () => void } => ({ addArc: () => undefined }),
  },
  XYWHRect: (x: number, y: number, width: number, height: number): { x: number; y: number; width: number; height: number } => ({
    x,
    y,
    width,
    height,
  }),
};
