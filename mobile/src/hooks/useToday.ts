import { useEffect, useState } from 'react';
import { startOfDay } from '../utils/agenda/dates';

/** Milliseconds between two looks at the clock to see whether the day changed. */
const CHECK_INTERVAL_MS = 30_000;

/**
 * Gives the midnight that starts today, and changes only when the day does. A screen that marks today, or draws a line at the current
 * time, uses it so it moves on at midnight without drawing again every second. The same object is returned until the day changes.
 * @returns The midnight of today in the time of the phone.
 */
export function useToday(): Date {
  const [today, setToday] = useState(() => startOfDay(new Date()));

  useEffect(() => {
    const timer = setInterval(() => {
      const now = startOfDay(new Date());
      setToday((previous) => (previous.getTime() === now.getTime() ? previous : now));
    }, CHECK_INTERVAL_MS);
    return () => clearInterval(timer);
  }, []);

  return today;
}
