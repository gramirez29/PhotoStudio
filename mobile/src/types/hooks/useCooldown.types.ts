/** What `useCooldown` returns: the seconds left to wait and how to start the wait. */
export interface CooldownState {
  /** Seconds left before the user may try again; zero when they may. */
  readonly remaining: number;
  /** Starts (or restarts) the wait. */
  readonly start: (seconds: number) => void;
}
