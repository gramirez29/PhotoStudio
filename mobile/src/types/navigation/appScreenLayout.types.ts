import type { ReactNode } from 'react';

/** What the navigator hands to `appScreenLayout` for each screen: the screen itself. */
export interface AppScreenLayoutArgs {
  /** Content of the screen. */
  readonly children: ReactNode;
}
