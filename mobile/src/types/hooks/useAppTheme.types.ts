import type { MD3Theme } from 'react-native-paper';
import type { ColorScheme, Palette } from '../theme/palette.types';

/** What `useAppTheme` gives a component: the scheme being shown and the colors and theme that go with it. */
export interface AppTheme {
  /** Color scheme being shown: light or dark. */
  readonly scheme: ColorScheme;
  /** Colors of that scheme. */
  readonly palette: Palette;
  /** The same theme in the form the component library asks for. */
  readonly paperTheme: MD3Theme;
}
