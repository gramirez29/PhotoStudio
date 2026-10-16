/** State and actions to share the booking link, as returned by `useShareLink`. */
export interface ShareLinkState {
  /** True while the link is being created. */
  readonly isPending: boolean;
  /** Message for the last failed attempt, or null. */
  readonly errorMessage: string | null;
  /** Asks for confirmation and, when the photographer accepts, creates the link and opens WhatsApp with it. */
  readonly share: () => void;
}
