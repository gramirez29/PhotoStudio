import type { ReactElement } from 'react';
import type { PageShellProps } from '../types/components/PageShell.types';

/**
 * Frame of every screen: the brand on top and a centered column that reads well on a phone and on a desktop.
 * @param props Component props.
 * @returns The frame.
 */
export function PageShell({ children }: PageShellProps): ReactElement {
  return (
    <main className="page">
      <div className="brand">PhotoSense Studio</div>
      {children}
    </main>
  );
}
