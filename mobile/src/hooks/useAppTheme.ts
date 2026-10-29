import { useMemo } from 'react';
import { useColorScheme } from 'react-native';
import { useAppearanceStore } from '../appearance/appearanceStore';
import { getPalette } from '../theme/palettes';
import { buildPaperTheme } from '../theme/paperTheme';
import type { AppTheme } from '../types/hooks/useAppTheme.types';
import { resolveColorScheme } from '../utils/appearance';

/**
 * The theme the app shows right now: what the photographer chose, or what the phone says when they chose to follow it. It
 * changes as soon as either does.
 * @returns The scheme, its palette and its theme for the component library.
 */
export function useAppTheme(): AppTheme {
  const preference = useAppearanceStore((state) => state.preference);
  const system = useColorScheme();
  const scheme = resolveColorScheme(preference, system);

  return useMemo(() => ({ scheme, palette: getPalette(scheme), paperTheme: buildPaperTheme(scheme) }), [scheme]);
}
