import type { ReactElement } from 'react';

/**
 * Shown for an address that is neither the main page nor a booking link. It sends the visitor to the main page with a plain
 * link, because the site has no other pages to suggest.
 * @returns The explanation.
 */
export function NotFoundScreen(): ReactElement {
  return (
    <div className="state">
      <h1>Esta página no existe</h1>
      <p className="muted">
        Revisa que el enlace esté completo o <a href="/">ve a la página principal</a>.
      </p>
    </div>
  );
}
