import type { ReactElement } from 'react';
import type { LoadErrorScreenProps } from '../types/components/LoadErrorScreen.types';

/**
 * Explains why the booking could not be shown and, when trying again can help, offers to do so.
 * @param props Component props.
 * @returns The explanation.
 */
export function LoadErrorScreen({ view, onRetry }: LoadErrorScreenProps): ReactElement {
  return (
    <div className="state" role="alert">
      <h1>{view.title}</h1>
      <p className="muted">{view.message}</p>
      {view.canRetry && (
        <button type="button" className="button" onClick={onRetry}>
          Intentar de nuevo
        </button>
      )}
    </div>
  );
}
