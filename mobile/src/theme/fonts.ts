import { DMSans_400Regular, DMSans_500Medium, DMSans_700Bold } from '@expo-google-fonts/dm-sans';
import { JosefinSans_600SemiBold, JosefinSans_700Bold } from '@expo-google-fonts/josefin-sans';

/**
 * Names of the font families of the app, the same two the web portal uses: Josefin Sans for titles and DM Sans for text. On
 * Android a weight is a family of its own, so every weight has its own name.
 */
export const FONT_FAMILIES = {
  /** Titles and headings. */
  title: 'JosefinSans_600SemiBold',
  /** Titles that need more weight. */
  titleBold: 'JosefinSans_700Bold',
  /** Running text. */
  body: 'DMSans_400Regular',
  /** Labels, buttons and emphasis inside a text. */
  bodyMedium: 'DMSans_500Medium',
  /** Strong emphasis, such as an amount. */
  bodyBold: 'DMSans_700Bold',
} as const;

/** The font files the app loads at start, by family name. They ship inside the app: no download, no network. */
export const FONT_ASSETS = {
  [FONT_FAMILIES.title]: JosefinSans_600SemiBold,
  [FONT_FAMILIES.titleBold]: JosefinSans_700Bold,
  [FONT_FAMILIES.body]: DMSans_400Regular,
  [FONT_FAMILIES.bodyMedium]: DMSans_500Medium,
  [FONT_FAMILIES.bodyBold]: DMSans_700Bold,
};
