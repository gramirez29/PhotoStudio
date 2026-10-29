import type { ReactElement } from 'react';
import { PaperProvider } from 'react-native-paper';
import { useAppTheme } from '../hooks/useAppTheme';
import type { AppThemeProviderProps } from '../types/components/AppThemeProvider.types';

/**
 * Gives every component the theme of the app: the library's components read their colors and fonts from it, and it follows the
 * choice of the photographer.
 * @param props Component props.
 * @returns The app wrapped in the theme.
 */
export function AppThemeProvider({ children }: AppThemeProviderProps): ReactElement {
  const { paperTheme } = useAppTheme();

  return <PaperProvider theme={paperTheme}>{children}</PaperProvider>;
}
