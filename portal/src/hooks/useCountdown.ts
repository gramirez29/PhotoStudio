import { useCallback, useEffect, useState } from 'react';
import type { Countdown } from '../types/hooks/useCountdown.types';

/**
 * Counts down whole seconds to zero. It is computed from the instant the countdown ends, not by subtracting one per tick, so a
 * tab that sleeps in the background shows the right time when it wakes up.
 * @returns The seconds left (0 when idle or finished) and the function that starts a countdown.
 */
export function useCountdown(): Countdown {
  const [endsAt, setEndsAt] = useState<number | null>(null);
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (endsAt === null) {
      return undefined;
    }

    const timer = window.setInterval(() => setNow(Date.now()), 250);
    return () => window.clearInterval(timer);
  }, [endsAt]);

  const start = useCallback((seconds: number) => {
    const current = Date.now();
    setNow(current);
    setEndsAt(current + Math.max(0, Math.ceil(seconds)) * 1000);
  }, []);

  const remaining = endsAt === null ? 0 : Math.max(0, Math.ceil((endsAt - now) / 1000));
  return { remaining, start };
}
