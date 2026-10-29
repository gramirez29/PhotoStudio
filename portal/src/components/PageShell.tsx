import type { ReactElement } from 'react';
import logoMark from '../assets/logo-mark.png';
import type { PageShellProps } from '../types/components/PageShell.types';
import { EnvironmentBanner } from './EnvironmentBanner';

/**
 * Frame of every screen: the test-site strip when there is one, the band of the studio with its logo on top and a centered
 * column that reads well on a phone and on a desktop.
 * @param props Component props.
 * @returns The frame.
 */
export function PageShell({ children }: PageShellProps): ReactElement {
  return (
    <>
      <EnvironmentBanner />
      <header className="site-header">
        <div className="site-header__inner">
          <img className="site-header__logo" src={logoMark} alt="" width={44} height={44} />
          <span className="site-header__name">PhotoSense Studio</span>
        </div>
      </header>
      <main className="page">{children}</main>
    </>
  );
}
