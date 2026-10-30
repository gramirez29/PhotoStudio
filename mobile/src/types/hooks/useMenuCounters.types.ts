/** What the menu needs to say about what is waiting for the photographer. */
export interface MenuCounters {
  /** Notices not read yet. */
  readonly unreadCount: number;
  /** Refunds the photographer still has to give back. */
  readonly pendingRefunds: number;
  /** Whether something is waiting: a notice or a refund. The button of the menu says so with a dot. */
  readonly needsAttention: boolean;
}
