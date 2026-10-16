import type { ReactElement } from 'react';
import { testEnvironment } from '../config/env';

/**
 * Strip on top of the page that says this is a test site, so nobody mistakes it for the real one or trusts its data. It is
 * shown only on test builds; the production build renders nothing.
 * @returns The strip, or nothing in production.
 */
export function EnvironmentBanner(): ReactElement | null {
  if (testEnvironment === null) {
    return null;
  }

  return (
    <div className="environment-banner" role="note">
      Ambiente de pruebas ({testEnvironment}). Los datos no son reales.
    </div>
  );
}
