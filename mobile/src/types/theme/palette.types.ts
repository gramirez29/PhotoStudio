/** The two color schemes of the app. */
export type ColorScheme = 'light' | 'dark';

/** What the photographer chose for the appearance: follow the phone, or always light, or always dark. */
export type ThemePreference = 'system' | ColorScheme;

/**
 * The colors of one theme, named after the roles of Material Design 3 so they map straight onto the theme of the component
 * library. Components never write a color: they read it from here, through the theme.
 */
export interface Palette {
  /** Main action color: filled buttons, the selected state. */
  readonly primary: string;
  /** Text and icons on `primary`. */
  readonly onPrimary: string;
  /** Soft version of the main color, for tonal buttons and highlights. */
  readonly primaryContainer: string;
  /** Text and icons on `primaryContainer`. */
  readonly onPrimaryContainer: string;
  /** Second accent of the brand (the slate blue of the logo in the light theme). */
  readonly secondary: string;
  /** Text and icons on `secondary`. */
  readonly onSecondary: string;
  /** Soft version of the second accent. */
  readonly secondaryContainer: string;
  /** Text and icons on `secondaryContainer`. */
  readonly onSecondaryContainer: string;
  /** Color of the screens. */
  readonly background: string;
  /** Text and icons on the screens. */
  readonly onBackground: string;
  /** Color of sheets and menus. */
  readonly surface: string;
  /** Text and icons on `surface`. */
  readonly onSurface: string;
  /** Color of cards and the other tonal blocks. */
  readonly surfaceVariant: string;
  /** Secondary text and icons on the tonal blocks and on the screens. */
  readonly onSurfaceVariant: string;
  /** Borders that must be seen, such as the one of a text field (3:1 against the background). */
  readonly outline: string;
  /** Soft borders and dividers. */
  readonly outlineVariant: string;
  /** Errors and destructive actions. */
  readonly error: string;
  /** Text and icons on `error`. */
  readonly onError: string;
  /** Soft background of an error message. */
  readonly errorContainer: string;
  /** Text and icons on `errorContainer`. */
  readonly onErrorContainer: string;
  /** Confirmed, paid, done. */
  readonly success: string;
  /** Something that needs attention. */
  readonly warning: string;
  /** Neutral information. */
  readonly info: string;
  /** Placeholders and what is no longer active. */
  readonly muted: string;
  /** Shade behind dialogs and sheets. */
  readonly scrim: string;
  /** The slate blue of the logo. */
  readonly brand: string;
  /** Text and icons on `brand`. */
  readonly onBrand: string;
  /** The silver of the logo, for borders. */
  readonly silver: string;
}

/** The roles of the palette that mark a state (a booking status, a result): each one is a color of the theme. */
export type StatusColorRole = 'success' | 'warning' | 'info' | 'muted' | 'error';
