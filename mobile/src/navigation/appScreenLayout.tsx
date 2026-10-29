import type { ReactElement } from 'react';
import { AppBackground } from '../components/AppBackground';
import type { AppScreenLayoutArgs } from '../types/navigation/appScreenLayout.types';

/**
 * Wraps every screen of the navigator in the background of the app, so no screen has to paint it. It is the `screenLayout` of
 * the stack: each screen carries its own copy, which is what keeps a screen opaque while another slides over it.
 * @param args The screen to wrap.
 * @returns The screen on top of the background.
 */
export function appScreenLayout({ children }: AppScreenLayoutArgs): ReactElement {
  return <AppBackground>{children}</AppBackground>;
}
