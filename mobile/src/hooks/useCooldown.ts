import { useCallback, useEffect, useState } from 'react';
import type { CooldownState } from '../types/hooks/useCooldown.types';

/**
 * Counts down the seconds a user has to wait before doing something again, such as asking for another code. It ticks once a
 * second only while there is something left, and stops by itself.
 * @returns The seconds left and the function that starts the wait.
 */
export function useCooldown(): CooldownState {
  const [remaining, setRemaining] = useState(0);

  useEffect(() => {
    if (remaining <= 0) {
      return undefined;
    }

    const timer = setTimeout(() => setRemaining((seconds) => Math.max(0, seconds - 1)), 1000);
    return () => clearTimeout(timer);
  }, [remaining]);

  const start = useCallback((seconds: number) => setRemaining(Math.max(0, Math.ceil(seconds))), []);
  return { remaining, start };
}
