import type { ReactElement } from 'react';
import type { PageShellProps } from '../types/components/PageShell.types';
import { EnvironmentBanner } from './EnvironmentBanner';

/**
 * Frame of every screen: the test-site strip when there is one, the brand on top and a centered column that reads well on a
 * phone and on a desktop.
 * @param props Component props.
 * @returns The frame.
 */
export function PageShell({ children }: PageShellProps): ReactElement {
  return (
    <>
      <EnvironmentBanner />
      <main className="page">
        <div className="brand">PhotoSense Studio</div>
        {children}
      </main>
    </>
  );
}
