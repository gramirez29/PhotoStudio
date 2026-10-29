/** Every way the photographer can choose the appearance of the app, in the order the selector shows them. */
export const THEME_PREFERENCES = ['system', 'light', 'dark'] as const;

/**
 * What the app uses until the photographer chooses another. It is the light theme, not the setting of the phone, so the app
 * looks the same for everyone until they ask for something else.
 */
export const DEFAULT_THEME_PREFERENCE = 'light';
