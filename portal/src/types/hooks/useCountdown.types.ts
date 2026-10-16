/** Result of `useCountdown`. */
export interface Countdown {
  /** Whole seconds left; 0 when no countdown is running or it finished. */
  readonly remaining: number;
  /** Starts (or restarts) a countdown of that many seconds. */
  readonly start: (seconds: number) => void;
}
