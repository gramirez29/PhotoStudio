import { Linking } from 'react-native';

/**
 * Opens a link in the app that handles it (WhatsApp for `wa.me` links, which falls back to the browser when WhatsApp is
 * not installed).
 * @param url Link to open.
 * @returns True when the link was opened, false when the device could not open it.
 */
export async function openExternalUrl(url: string): Promise<boolean> {
  try {
    await Linking.openURL(url);
    return true;
  } catch {
    return false;
  }
}
