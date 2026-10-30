import type { ReactElement } from 'react';
import type { LoadingScreenProps } from '../types/components/LoadingScreen.types';

/**
 * Shown while something loads. Announced to screen readers as a status.
 * @param props Component props.
 * @returns The message.
 */
export function LoadingScreen({ message = 'Cargando tu reserva…' }: LoadingScreenProps): ReactElement {
  return (
    <div className="state" role="status" aria-live="polite">
      <p className="muted">{message}</p>
    </div>
  );
}
