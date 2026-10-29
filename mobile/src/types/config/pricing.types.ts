import type { PackageTier } from '../api/booking';

/** A package the photographer sells, as set in `src/config/pricing.ts`. */
export interface PackageOption {
  /** Package the option stands for; this is what the backend stores. */
  readonly tier: PackageTier;
  /** Name shown in the drop-down. */
  readonly label: string;
  /** Price in colones the form fills in when the package is picked, or null when the photographer sets it case by case. */
  readonly price: number | null;
  /** Session length in hours the form selects when the package is picked, or null when the photographer chooses it. */
  readonly durationHours: number | null;
}

/** An extra the photographer sells on top of a package, as set in `src/config/pricing.ts`. */
export interface ExtraOption {
  /** Stable code of the extra. It is stored with the booking: do not change it once bookings use it. */
  readonly code: string;
  /** Name shown in the list of extras and stored with the booking. */
  readonly label: string;
  /** Price in colones of one unit. */
  readonly unitPrice: number;
}
