import { useEffect, useState } from 'react';

/**
 * Gives the current instant and renews it on an interval, so a line that marks the time of day moves on its own.
 * @param intervalMs Milliseconds between two renewals.
 * @returns The current instant, as of the last renewal.
 */
export function useNow(intervalMs: number): Date {
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    const timer = setInterval(() => setNow(new Date()), intervalMs);
    return () => clearInterval(timer);
  }, [intervalMs]);

  return now;
}
