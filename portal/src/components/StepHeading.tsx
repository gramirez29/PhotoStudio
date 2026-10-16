import { useEffect, useRef, type ReactElement } from 'react';
import type { StepHeadingProps } from '../types/components/StepHeading.types';

/**
 * Heading of a step of the proof flow. It takes the focus when the step appears, so a screen reader announces where the client
 * is and keyboard users continue from there.
 * @param props Component props.
 * @returns The heading.
 */
export function StepHeading({ children }: StepHeadingProps): ReactElement {
  const ref = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    ref.current?.focus();
  }, []);

  return (
    <h2 id="proof-step-title" ref={ref} tabIndex={-1}>
      {children}
    </h2>
  );
}
