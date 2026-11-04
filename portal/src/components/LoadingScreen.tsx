import type { ReactElement } from 'react';
import type { LoadingScreenProps } from '../types/components/LoadingScreen.types';

/**
 * Shown while something loads: a spinner with the message, centered in the page. Announced to screen readers as a status; the
 * spinner is decorative.
 * @param props Component props.
 * @returns The loader and the message.
 */
export function LoadingScreen({ message = 'Cargando tu reserva…' }: LoadingScreenProps): ReactElement {
  return (
    <div className="loading-screen" role="status" aria-live="polite">
      <span className="loading-screen__spinner" aria-hidden="true" />
      <p className="muted">{message}</p>
    </div>
  );
}
