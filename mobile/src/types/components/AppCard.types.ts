import type { ReactNode } from 'react';
import type { StyleProp, ViewStyle } from 'react-native';

/** Props of the `AppCard` component. */
export interface AppCardProps {
  /** What the card holds. */
  readonly children: ReactNode;
  /** Layout of what is inside the card (gap, direction, alignment); the colors, the corners and the padding are the card's. */
  readonly style?: StyleProp<ViewStyle>;
}
