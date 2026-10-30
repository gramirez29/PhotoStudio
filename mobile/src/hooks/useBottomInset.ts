import { useContext } from 'react';
import { SafeAreaInsetsContext } from 'react-native-safe-area-context';

/**
 * Height of what the system draws over the bottom edge of the screen (the navigation bar), so a screen that scrolls can leave
 * that much room at its end and its last element is never under it. The app is drawn from edge to edge on Android. Where no
 * safe area is provided, such as in a test, there is no inset.
 * @returns The inset in density-independent pixels; 0 when the system draws nothing there.
 */
export function useBottomInset(): number {
  return useContext(SafeAreaInsetsContext)?.bottom ?? 0;
}
