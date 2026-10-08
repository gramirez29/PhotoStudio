import * as SecureStore from 'expo-secure-store';

/** Key under which the refresh token is kept in the secure storage of the device. */
export const REFRESH_TOKEN_KEY = 'photostudio.refreshToken';

/**
 * Saves the refresh token in the secure storage of the device (Keychain on iOS, Keystore on Android). A failure is
 * swallowed: the session keeps working in memory and the photographer only has to sign in again after restarting the app.
 * @param token Refresh token to keep.
 * @returns A promise that resolves when the attempt finished.
 */
export async function saveRefreshToken(token: string): Promise<void> {
  try {
    await SecureStore.setItemAsync(REFRESH_TOKEN_KEY, token);
  } catch {
    // Secure storage can be unavailable (for example, a locked device); nothing useful can be done here.
  }
}

/**
 * Reads the saved refresh token.
 * @returns The token, or null when none is saved or the storage cannot be read.
 */
export async function loadRefreshToken(): Promise<string | null> {
  try {
    return await SecureStore.getItemAsync(REFRESH_TOKEN_KEY);
  } catch {
    return null;
  }
}

/**
 * Deletes the saved refresh token. A failure is swallowed, like in {@link saveRefreshToken}.
 * @returns A promise that resolves when the attempt finished.
 */
export async function clearRefreshToken(): Promise<void> {
  try {
    await SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY);
  } catch {
    // Nothing useful can be done if the storage cannot be written.
  }
}
