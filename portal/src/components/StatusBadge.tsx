import type { ReactElement } from 'react';
import type { StatusBadgeProps } from '../types/components/StatusBadge.types';

/**
 * Pill with a status in words and a colored dot. The color only reinforces the text, so the status is readable without it.
 * @param props Component props.
 * @returns The pill.
 */
export function StatusBadge({ label, tone }: StatusBadgeProps): ReactElement {
  return (
    <span className="status" data-tone={tone}>
      {label}
    </span>
  );
}
