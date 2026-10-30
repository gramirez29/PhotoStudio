import { useContext } from 'react';
import { SafeAreaInsetsContext } from 'react-native-safe-area-context';

/**
 * Height of what the system draws over the top edge of the screen (the status bar), for what is drawn from edge to edge, such as
 * the side menu. Where no safe area is provided, such as in a test, there is no inset.
 * @returns The inset in density-independent pixels; 0 when the system draws nothing there.
 */
export function useTopInset(): number {
  return useContext(SafeAreaInsetsContext)?.top ?? 0;
}
