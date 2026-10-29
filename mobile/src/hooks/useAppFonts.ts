import { useFonts } from 'expo-font';
import { FONT_ASSETS } from '../theme/fonts';

/**
 * Loads the fonts of the app (Josefin Sans and DM Sans). They ship inside the app, so this takes a moment and needs no network.
 * @returns True once the fonts are ready, or once loading them failed: the app opens with the system font instead of staying
 * on the splash screen.
 */
export function useAppFonts(): boolean {
  const [loaded, error] = useFonts(FONT_ASSETS);
  return loaded || error !== null;
}
