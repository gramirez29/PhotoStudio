import { Linking, Share } from 'react-native';

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

/**
 * Opens the share sheet of the system with a text, which lets the photographer copy it or send it with any app. It uses what the
 * system already has, so it needs no extra permission and no new build of the app.
 * @param message The text to share.
 * @returns True when the sheet opened, false when the system could not show it.
 */
export async function shareText(message: string): Promise<boolean> {
  try {
    await Share.share({ message });
    return true;
  } catch {
    return false;
  }
}
