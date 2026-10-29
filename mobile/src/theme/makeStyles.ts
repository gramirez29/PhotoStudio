import { useMemo } from 'react';
import { StyleSheet } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import type { StylesFactory } from '../types/theme/makeStyles.types';

/**
 * Makes the hook that gives a component its styles from the colors of the theme. A `StyleSheet.create` at the top of a file
 * is written once and never learns that the theme changed; this is the same thing written as a function of the palette, so the
 * styles follow the light and dark themes and are rebuilt only when the theme changes.
 *
 * Usage, at the bottom of the component file: `const useStyles = makeStyles((palette) => ({ title: { color: palette.onSurface } }));`
 * and, inside the component, `const styles = useStyles();`.
 * @param factory Function that writes the styles from the palette and the scheme.
 * @returns A hook that returns the styles of the theme being shown.
 */
export function makeStyles<T extends StyleSheet.NamedStyles<T>>(factory: StylesFactory<T>): () => T {
  return function useStyles(): T {
    const { palette, scheme } = useAppTheme();
    return useMemo(() => StyleSheet.create(factory(palette, scheme)), [palette, scheme]);
  };
}
