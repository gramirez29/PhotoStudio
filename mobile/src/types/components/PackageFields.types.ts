import type { PackageTier } from '../api/booking';

/** Props of the `PackageFields` component. */
export interface PackageFieldsProps {
  /** Package chosen, or null while none is. */
  readonly packageTier: PackageTier | null;
  /** Price of the package as typed, in colones. */
  readonly price: string;
  /** Validation message of the package, if any. */
  readonly packageError?: string;
  /** Validation message of the price, if any. */
  readonly priceError?: string;
  /** Called with the name of the package the photographer picks. */
  readonly onPackageChange: (label: string) => void;
  /** Called with the price the photographer types. */
  readonly onPriceChange: (text: string) => void;
}
