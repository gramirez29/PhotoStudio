import type { ReactElement } from 'react';

/**
 * Shown while the booking loads. Announced to screen readers as a status.
 * @returns The message.
 */
export function LoadingScreen(): ReactElement {
  return (
    <div className="state" role="status" aria-live="polite">
      <p className="muted">Cargando tu reserva…</p>
    </div>
  );
}
