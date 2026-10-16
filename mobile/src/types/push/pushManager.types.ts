/** What happened when the app tried to register the phone for push notifications. */
export type PushRegistrationOutcome =
  /** The backend knows the phone and will push to it. */
  | 'registered'
  /** The photographer did not allow notifications, so there is nothing to register. */
  | 'denied'
  /** This device cannot receive push (an emulator, or the build has no Expo project). */
  | 'unavailable'
  /** Something failed (no network, the backend refused); the next sign-in or app start tries again. */
  | 'failed';

/** What the app does when a push event happens. */
export interface PushEventHandlers {
  /** Called when a notification arrives while the app is open. */
  readonly onReceived: () => void;
  /** Called when the photographer taps a notification that is about a booking. */
  readonly onOpenBooking: (bookingId: string) => void;
}
