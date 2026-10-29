/** An extra chosen for a booking, with the price it was chosen at. */
export interface SelectedExtra {
  /** Stable code of the extra. */
  readonly code: string;
  /** Name of the extra. */
  readonly name: string;
  /** Price in colones of one unit. */
  readonly unitPrice: number;
  /** Number of units, at least 1. */
  readonly quantity: number;
}

/** What a booking costs, worked out from the price typed, the extras chosen and the discount typed. All in colones. */
export interface BookingTotals {
  /** Price of the package alone; zero while the price is empty or invalid. */
  readonly packagePrice: number;
  /** Sum of the extras. */
  readonly extrasTotal: number;
  /** Discount; zero while the field is empty or invalid. */
  readonly discount: number;
  /** What the client pays: package plus extras minus discount, never below zero. */
  readonly total: number;
}
