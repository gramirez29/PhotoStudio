import type { ReactElement } from 'react';

/**
 * Shown when the page is opened without a booking link, for example at the root of the site.
 * @returns The explanation.
 */
export function NoLinkScreen(): ReactElement {
  return (
    <div className="state">
      <h1>Abre el enlace de tu reserva</h1>
      <p className="muted">Esta página se abre desde el enlace que te envió tu fotógrafo por WhatsApp.</p>
    </div>
  );
}
